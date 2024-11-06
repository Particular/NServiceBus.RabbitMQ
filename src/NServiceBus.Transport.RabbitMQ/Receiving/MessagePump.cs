namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using BitFaster.Caching.Lru;
    using Extensibility;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using global::RabbitMQ.Client.Exceptions;
    using Logging;
    using NServiceBus.Transport.RabbitMQ.ManagementApi;
    using NServiceBus.Transport.RabbitMQ.ManagementApi.Models;

    sealed partial class MessagePump : IMessageReceiver
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(MessagePump));
        static readonly TransportTransaction transportTransaction = new();

        readonly ReceiveSettings settings;
        readonly ConnectionFactory connectionFactory;
        readonly MessageConverter messageConverter;
        readonly string consumerTag;
        readonly ChannelProvider channelProvider;
        readonly IManagementApi managementApi;
        readonly TimeSpan timeToWaitBeforeTriggeringCircuitBreaker;
        readonly QueuePurger queuePurger;
        readonly PrefetchCountCalculation prefetchCountCalculation;
        readonly Action<string, Exception, CancellationToken> criticalErrorAction;
        readonly TimeSpan retryDelay;
        readonly string name;
        readonly FastConcurrentLru<string, int> deliveryAttempts = new(1_000);
        readonly FastConcurrentLru<string, bool> failedBasicAckMessages = new(1_000);

        OnMessage onMessage;
        OnError onError;
        int maxConcurrency;
        long numberOfMessagesBeingProcessed;
        CancellationTokenSource messagePumpCancellationTokenSource;
        CancellationTokenSource messageProcessingCancellationTokenSource;
        MessagePumpConnectionFailedCircuitBreaker circuitBreaker;
        IConnection connection;
        Overview overview;
        Version brokerVersion;

        // Stop
        TaskCompletionSource connectionShutdownCompleted;

        public MessagePump(
            ReceiveSettings settings,
            ConnectionFactory connectionFactory,
            IRoutingTopology routingTopology,
            MessageConverter messageConverter,
            string consumerTag,
            ChannelProvider channelProvider,
            IManagementApi managementApi,
            TimeSpan timeToWaitBeforeTriggeringCircuitBreaker,
            PrefetchCountCalculation prefetchCountCalculation,
            Action<string, Exception, CancellationToken> criticalErrorAction,
            TimeSpan retryDelay)
        {
            this.settings = settings;
            this.connectionFactory = connectionFactory;
            this.messageConverter = messageConverter;
            this.consumerTag = consumerTag;
            this.channelProvider = channelProvider;
            this.managementApi = managementApi;
            this.timeToWaitBeforeTriggeringCircuitBreaker = timeToWaitBeforeTriggeringCircuitBreaker;
            this.prefetchCountCalculation = prefetchCountCalculation;
            this.criticalErrorAction = criticalErrorAction;
            this.retryDelay = retryDelay;

            ReceiveAddress = RabbitMQTransportInfrastructure.TranslateAddress(settings.ReceiveAddress);

            if (settings.UsePublishSubscribe)
            {
                Subscriptions = new SubscriptionManager(connectionFactory, routingTopology, ReceiveAddress);
            }

            queuePurger = new QueuePurger(connectionFactory);

            name = $"{ReceiveAddress} MessagePump";
        }

        public ISubscriptionManager Subscriptions { get; }
        public string Id => settings.Id;

        public string ReceiveAddress { get; }

        public async Task Initialize(PushRuntimeSettings limitations, OnMessage onMessage, OnError onError, CancellationToken cancellationToken = default)
        {
            this.onMessage = onMessage;
            this.onError = onError;
            maxConcurrency = limitations.MaxConcurrency;

            await CheckConnectivity(cancellationToken).ConfigureAwait(false);

            if (settings.PurgeOnStartup)
            {
                await queuePurger.Purge(ReceiveAddress, cancellationToken).ConfigureAwait(false);
            }

            await ValidateDeliveryLimit(cancellationToken).ConfigureAwait(false);
        }

        internal async Task CheckConnectivity(CancellationToken cancellationToken = default)
        {
            var response = await managementApi.GetOverview(cancellationToken).ConfigureAwait(false);
            if (response.HasValue)
            {
                overview = response.Value;
                brokerVersion = overview.RabbitMqVersion;
            }
            else
            {
                // TODO: Need logic/config settings for determining which action to take if management API unavailable, e.g. should we throw an exception to refuse to start, or just log a warning
                Logger.WarnFormat("Could not access RabbitMQ Management API. ({0}: {1})", response.StatusCode, response.Reason);

                using var connection = await connectionFactory.CreateAdministrationConnection(cancellationToken).ConfigureAwait(false);
                brokerVersion = connection.GetBrokerVersion();
            }
        }

        bool HasManagementApiAccess => overview != null;

        async Task ValidateDeliveryLimit(CancellationToken cancellationToken)
        {
            if (!HasManagementApiAccess)
            {
                return;
            }

            var response = await managementApi.GetQueue(ReceiveAddress, cancellationToken).ConfigureAwait(false);
            if (!response.HasValue)
            {
                // TODO: Need logic/config settings for determining which action to take, e.g. should we throw an exception to refuse to start, or just log a warning
                Logger.WarnFormat("Could not determine delivery limit for {0}. ({1}: {2})", ReceiveAddress, response.StatusCode, response.Reason);
                return;
            }

            var queue = response.Value;
            if (queue.DeliveryLimit == -1)
            {
                return;
            }

            if (queue.Arguments.DeliveryLimit.HasValue &&
                queue.Arguments.DeliveryLimit != -1)
            {
                // TODO: Need logic/config settings for determining which action to take, e.g. should we throw an exception to refuse to start, or just log a warning
                Logger.WarnFormat("The delivery limit for {0} is set to {1} by a queue argument. This can interfere with the transport's retry implementation",
                    queue.Name, queue.Arguments.DeliveryLimit);
                return;
            }

            if (queue.EffectivePolicyDefinition.DeliveryLimit.HasValue &&
                queue.EffectivePolicyDefinition.DeliveryLimit != -1)
            {
                // TODO: Need logic/config settings for determining which action to take, e.g. should we throw an exception to refuse to start, or just log a warning
                Logger.WarnFormat("The RabbitMQ policy {2} is setting delivery limit to {1} for {0}.",
                    queue.Name, queue.EffectivePolicyDefinition.DeliveryLimit, queue.AppliedPolicyName);
                return;
            }

            await SetDeliveryLimitViaPolicy(queue, cancellationToken).ConfigureAwait(false);
        }

        async Task SetDeliveryLimitViaPolicy(Queue queue, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(queue.AppliedPolicyName))
            {
                // TODO: Need logic/config settings for determining which action to take, e.g. should we throw an exception to refuse to start, or just log a warning
                Logger.WarnFormat("The {0} queue already has an associated policy.", queue.Name, queue.AppliedPolicyName);
                return;
            }

            if (brokerVersion.Major < 4)
            {
                // TODO: Need logic/config settings for determining which action to take, e.g. should we throw an exception to refuse to start, or just log a warning
                Logger.WarnFormat("Cannot override delivery limit on the {0} queue by policy in RabbitMQ versions prior to 4.", queue.Name);
                return;
            }

            var policy = new Policy
            {
                Name = $"nsb.{queue.Name}.delivery-limit",
                ApplyTo = PolicyTarget.QuorumQueues,
                Definition = new PolicyDefinition { DeliveryLimit = -1 },
                Pattern = queue.Name,
                Priority = 100
            };
            await managementApi.CreatePolicy(policy, cancellationToken).ConfigureAwait(false);
        }

        public async Task StartReceive(CancellationToken cancellationToken = default)
        {
            messagePumpCancellationTokenSource = new CancellationTokenSource();
            messageProcessingCancellationTokenSource = new CancellationTokenSource();

            circuitBreaker = new MessagePumpConnectionFailedCircuitBreaker(
                name,
                timeToWaitBeforeTriggeringCircuitBreaker,
                (message, exception) => criticalErrorAction(message, exception, messageProcessingCancellationTokenSource.Token));

            await ConnectToBroker(cancellationToken).ConfigureAwait(false);
        }

        public async Task ChangeConcurrency(PushRuntimeSettings limitations, CancellationToken cancellationToken = default)
        {
            Logger.InfoFormat("Calling a change concurrency and reconnecting with new value {0}.", limitations.MaxConcurrency);

            await StopReceive(cancellationToken).ConfigureAwait(false);
            maxConcurrency = limitations.MaxConcurrency;
            await StartReceive(CancellationToken.None).ConfigureAwait(false);
        }

        async Task ConnectToBroker(CancellationToken cancellationToken)
        {
            connection = await connectionFactory.CreateConnection(name, cancellationToken).ConfigureAwait(false);
            connection.ConnectionShutdownAsync += Connection_ConnectionShutdown;

            var prefetchCount = prefetchCountCalculation(maxConcurrency);

            if (prefetchCount < maxConcurrency)
            {
                Logger.Warn($"The specified prefetch count '{prefetchCount}' is smaller than the specified maximum concurrency '{maxConcurrency}'. The maximum concurrency value will be used as the prefetch count instead.");
                prefetchCount = maxConcurrency;
            }

            var createChannelOptions = new CreateChannelOptions(publisherConfirmationsEnabled: false, publisherConfirmationTrackingEnabled: false, consumerDispatchConcurrency: (ushort)maxConcurrency);
            var channel = await connection.CreateChannelAsync(createChannelOptions, cancellationToken).ConfigureAwait(false);
            channel.ChannelShutdownAsync += Channel_ModelShutdown;
            await channel.BasicQosAsync(0, (ushort)Math.Min(prefetchCount, ushort.MaxValue), false, cancellationToken).ConfigureAwait(false);

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.UnregisteredAsync += Consumer_Unregistered;
            consumer.RegisteredAsync += Consumer_Registered;
            consumer.ReceivedAsync += Consumer_Received;

            await channel.BasicConsumeAsync(ReceiveAddress, false, consumerTag, consumer, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task StopReceive(CancellationToken cancellationToken = default)
        {
            if (messagePumpCancellationTokenSource is null)
            {
                // Receiver hasn't been started or is already stopped
                return;
            }

            messagePumpCancellationTokenSource?.Cancel();

            await using (cancellationToken.Register(() => messageProcessingCancellationTokenSource?.Cancel()))
            {
                while (Interlocked.Read(ref numberOfMessagesBeingProcessed) > 0)
                {
                    // We are deliberately not forwarding the cancellation token here because
                    // this loop is our way of waiting for all pending messaging operations
                    // to participate in cooperative cancellation or not.
                    // We do not want to rudely abort them because the cancellation token has been canceled.
                    // This allows us to preserve the same behaviour in v8 as in v7 in that,
                    // if CancellationToken.None is passed to this method,
                    // the method will only return when all in flight messages have been processed.
                    // If, on the other hand, a non-default CancellationToken is passed,
                    // all message processing operations have the opportunity to
                    // participate in cooperative cancellation.
                    // If we ever require a method of stopping the endpoint such that
                    // all message processing is canceled immediately,
                    // we can provide that as a separate feature.
                    await Task.Delay(50, CancellationToken.None).ConfigureAwait(false);
                }

                // RunContinuationsAsynchronously was chosen to make sure the completed event handler can return and the continuation
                // is not executed on the event handler thread
                connectionShutdownCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                if (connection.IsOpen)
                {
                    try
                    {
                        await connection.CloseAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        // We are catching the exception here to avoid the exception being thrown upwards
                        // The connection will get disposed further down anyway.
                        connectionShutdownCompleted.TrySetResult();
                    }
                }
                else
                {
                    connectionShutdownCompleted.TrySetResult();
                }

                await connectionShutdownCompleted.Task.ConfigureAwait(false);
            }

            messagePumpCancellationTokenSource?.Dispose();
            messagePumpCancellationTokenSource = null;
            messageProcessingCancellationTokenSource?.Dispose();
            connection.Dispose();
            circuitBreaker?.Dispose();
        }

#pragma warning disable PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
        Task Consumer_Registered(object sender, ConsumerEventArgs e)
#pragma warning restore PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
        {
            circuitBreaker.Success();

            return Task.CompletedTask;
        }

#pragma warning disable PS0018
        Task Connection_ConnectionShutdown(object sender, ShutdownEventArgs e)
#pragma warning restore PS0018
        {
            if (e.Initiator == ShutdownInitiator.Application && e.ReplyCode == 200)
            {
                connectionShutdownCompleted?.TrySetResult();
            }
            else if (circuitBreaker.Disarmed)
            {
                //log entry handled by event handler registered in ConnectionFactory
                circuitBreaker.Failure(new Exception(e.ToString()));
                _ = Task.Run(() => Reconnect(messageProcessingCancellationTokenSource.Token));
            }
            else
            {
                Logger.WarnFormat("'{0}' connection shutdown while reconnect already in progress: {1}", name, e);
            }

            return Task.CompletedTask;
        }

#pragma warning disable PS0018
        Task Channel_ModelShutdown(object sender, ShutdownEventArgs e)
#pragma warning restore PS0018
        {
            if (e.Initiator == ShutdownInitiator.Application)
            {
                return Task.CompletedTask;
            }

            if (e.Initiator == ShutdownInitiator.Peer && e.ReplyCode == 404)
            {
                return Task.CompletedTask;
            }

            if (circuitBreaker.Disarmed)
            {
                Logger.WarnFormat("'{0}' channel shutdown: {1}", name, e);
                circuitBreaker.Failure(new Exception(e.ToString()));
                _ = Task.Run(() => Reconnect(messageProcessingCancellationTokenSource.Token));
            }
            else
            {
                Logger.WarnFormat("'{0}' channel shutdown while reconnect already in progress: {1}", name, e);
            }

            return Task.CompletedTask;
        }

#pragma warning disable PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
        Task Consumer_Unregistered(object sender, ConsumerEventArgs e)
#pragma warning restore PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
        {
            var consumer = (AsyncEventingBasicConsumer)sender;

            if (consumer.Channel is not { IsOpen: true } || !connection.IsOpen)
            {
                return Task.CompletedTask;
            }

            if (circuitBreaker.Disarmed)
            {
                Logger.WarnFormat("'{0}' consumer canceled by broker", name);
                circuitBreaker.Failure(new Exception($"'{name}' consumer canceled by broker"));
                _ = Task.Run(() => Reconnect(messageProcessingCancellationTokenSource.Token));
            }
            else
            {
                Logger.WarnFormat("'{0}' consumer canceled by broker while reconnect already in progress", name);
            }

            return Task.CompletedTask;
        }

        async Task Reconnect(CancellationToken cancellationToken)
        {
            try
            {

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        if (connection.IsOpen)
                        {
                            await connection.CloseAsync(cancellationToken).ConfigureAwait(false);
                        }

                        connection.Dispose();

                        Logger.InfoFormat("'{0}': Attempting to reconnect in {1} seconds.", name, retryDelay.TotalSeconds);

                        await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);

                        await ConnectToBroker(cancellationToken).ConfigureAwait(false);
                        break;
                    }
                    catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
                    {
                        Logger.InfoFormat("'{0}': Reconnecting to the broker failed: {1}", name, ex);
                    }
                }

                Logger.InfoFormat("'{0}': Connection to the broker reestablished successfully.", name);
            }
            catch (Exception ex) when (ex.IsCausedBy(cancellationToken))
            {
                Logger.DebugFormat("'{0}': Reconnection canceled since the transport is being stopped: {1}", name, ex);
            }
            catch (Exception ex)
            {
                Logger.WarnFormat("'{0}': Unexpected error while reconnecting: '{1}'", name, ex);
            }
        }

#pragma warning disable PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
        async Task Consumer_Received(object sender, BasicDeliverEventArgs eventArgs)
#pragma warning restore PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
        {
            if (messagePumpCancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            var consumer = (AsyncEventingBasicConsumer)sender;

            await ProcessAndSwallowExceptions(consumer, eventArgs, messageProcessingCancellationTokenSource.Token).ConfigureAwait(false);
        }

        async Task ProcessAndSwallowExceptions(AsyncEventingBasicConsumer consumer, BasicDeliverEventArgs message, CancellationToken messageProcessingCancellationToken)
        {
            Interlocked.Increment(ref numberOfMessagesBeingProcessed);

            try
            {
                try
                {
                    await Process(consumer, message, messageProcessingCancellationToken).ConfigureAwait(false);
                }
#pragma warning disable PS0019 // Do not catch Exception without considering OperationCanceledException - handling is the same for OCE
                catch (Exception ex)
#pragma warning restore PS0019 // Do not catch Exception without considering OperationCanceledException
                {
                    Logger.Debug("Returning message to queue...", ex);
                    await consumer.Channel.BasicRejectAndRequeueIfOpen(message.DeliveryTag, cancellationToken: messageProcessingCancellationToken).ConfigureAwait(false);
                    throw;
                }
            }
            catch (Exception ex) when (ex.IsCausedBy(messageProcessingCancellationToken))
            {
                Logger.Debug("Message processing canceled.", ex);
            }
            catch (Exception ex)
            {
                Logger.Error("Message processing failed.", ex);
            }
            finally
            {
                Interlocked.Decrement(ref numberOfMessagesBeingProcessed);
            }
        }

        async Task Process(AsyncEventingBasicConsumer consumer, BasicDeliverEventArgs message, CancellationToken messageProcessingCancellationToken)
        {
            Dictionary<string, string> headers;

            try
            {
                headers = messageConverter.RetrieveHeaders(message);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to retrieve headers from poison message. Moving message to queue '{settings.ErrorQueue}'...", ex);
                await MovePoisonMessage(consumer, message, settings.ErrorQueue, messageProcessingCancellationToken).ConfigureAwait(false);

                return;
            }

            string messageId;

            try
            {
                messageId = messageConverter.RetrieveMessageId(message, headers);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to retrieve ID from poison message. Moving message to queue '{settings.ErrorQueue}'...", ex);
                await MovePoisonMessage(consumer, message, settings.ErrorQueue, messageProcessingCancellationToken).ConfigureAwait(false);

                return;
            }

            var messageIdKey = CreateMessageIdKey(headers, messageId);

            if (failedBasicAckMessages.TryGet(messageIdKey, out _))
            {
                try
                {
                    await consumer.Channel.BasicAckSingle(message.DeliveryTag, messageProcessingCancellationToken).ConfigureAwait(false);
                }
                catch (AlreadyClosedException ex)
                {
                    Logger.Warn($"Failed to acknowledge message '{messageId}' because the channel was closed. The message was returned to the queue.", ex);
                }

                return;
            }

            var processingContext = new ContextBag();
            processingContext.Set(message);

            try
            {
                var messageContext = new MessageContext(messageId, headers, message.Body, transportTransaction, ReceiveAddress, processingContext);
                await onMessage(messageContext, messageProcessingCancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!ex.IsCausedBy(messageProcessingCancellationToken))
            {
                var numberOfDeliveryAttempts = GetDeliveryAttempts(message, messageIdKey);
                headers = messageConverter.RetrieveHeaders(message);
                var errorContext = new ErrorContext(ex, headers, messageId, message.Body, transportTransaction, numberOfDeliveryAttempts, ReceiveAddress, processingContext);

                try
                {
                    var result = await onError(errorContext, messageProcessingCancellationToken).ConfigureAwait(false);

                    if (result == ErrorHandleResult.RetryRequired)
                    {
                        await consumer.Channel.BasicRejectAndRequeueIfOpen(message.DeliveryTag, messageProcessingCancellationToken).ConfigureAwait(false);
                        return;
                    }
                }
                catch (Exception onErrorEx) when (!onErrorEx.IsCausedBy(messageProcessingCancellationToken))
                {
                    criticalErrorAction($"Failed to execute recoverability policy for message with native ID: `{messageId}`", onErrorEx, messageProcessingCancellationToken);
                    await consumer.Channel.BasicRejectAndRequeueIfOpen(message.DeliveryTag, messageProcessingCancellationToken).ConfigureAwait(false);

                    return;
                }
            }

            try
            {
                await consumer.Channel.BasicAckSingle(message.DeliveryTag, cancellationToken: messageProcessingCancellationToken).ConfigureAwait(false);
            }
            catch (AlreadyClosedException ex)
            {
                failedBasicAckMessages.AddOrUpdate(messageIdKey, true);

                if (PreconditionFailedRegex().IsMatch(ex.ShutdownReason.ReplyText))
                {
                    Logger.Error($"Failed to acknowledge message '{messageId}' because the handler execution time exceeded the broker delivery acknowledgement timeout. Increase the length of the timeout on the broker. The message was returned to the queue.", ex);
                }
                else
                {
                    Logger.Warn($"Failed to acknowledge message '{messageId}' because the channel was closed. The message was returned to the queue.", ex);
                }
            }
        }

        static string CreateMessageIdKey(Dictionary<string, string> headers, string messageId)
        {
            var delayedRetries = headers.GetValueOrDefault(NServiceBus.Headers.DelayedRetries, "0");

            return $"{messageId}-{delayedRetries}";
        }

        int GetDeliveryAttempts(BasicDeliverEventArgs message, string messageIdKey)
        {
            var attempts = 1;

            if (!message.Redelivered)
            {
                return attempts;
            }

            if (message.BasicProperties.Headers != null && message.BasicProperties.Headers.TryGetValue("x-delivery-count", out var headerValue))
            {
                attempts = Convert.ToInt32(headerValue) + 1;
            }
            else
            {

                attempts = deliveryAttempts.GetOrAdd(messageIdKey, k => 1);
                attempts++;
                deliveryAttempts.AddOrUpdate(messageIdKey, attempts);
            }

            return attempts;
        }

        async ValueTask MovePoisonMessage(AsyncEventingBasicConsumer consumer, BasicDeliverEventArgs message, string queue, CancellationToken messageProcessingCancellationToken)
        {
            try
            {
                var channel = await channelProvider.GetPublishChannel(messageProcessingCancellationToken).ConfigureAwait(false);

                try
                {
                    await channel.RawSendInCaseOfFailure(queue, message.Body, new BasicProperties(message.BasicProperties), messageProcessingCancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    await channelProvider.ReturnPublishChannel(channel, messageProcessingCancellationToken)
                        .ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (!ex.IsCausedBy(messageProcessingCancellationToken))
            {
                Logger.Error($"Failed to move poison message to queue '{queue}'. Returning message to original queue...", ex);
                await consumer.Channel.BasicRejectAndRequeueIfOpen(message.DeliveryTag, cancellationToken: messageProcessingCancellationToken).ConfigureAwait(false);

                return;
            }

            try
            {
                await consumer.Channel.BasicAckSingle(message.DeliveryTag, cancellationToken: messageProcessingCancellationToken).ConfigureAwait(false);
            }
            catch (AlreadyClosedException ex)
            {
                Logger.Warn($"Failed to acknowledge poison message because the channel was closed. The message was sent to queue '{queue}' but also returned to the original queue.", ex);
            }
        }

        [GeneratedRegex(@"PRECONDITION_FAILED - delivery acknowledgement on channel [0-9]+ timed out\. Timeout value used: [0-9]+ ms\. This timeout value can be configured, see consumers doc guide to learn more")]
        private static partial Regex PreconditionFailedRegex();
    }
}
