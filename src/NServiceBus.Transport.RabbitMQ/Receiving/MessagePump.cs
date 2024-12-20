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

    sealed class MessagePump : IMessageReceiver, IDisposable
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(MessagePump));
        static readonly TransportTransaction transportTransaction = new();

        readonly ReceiveSettings settings;
        readonly ConnectionFactory connectionFactory;
        readonly MessageConverter messageConverter;
        readonly string consumerTag;
        readonly ChannelProvider channelProvider;
        readonly TimeSpan timeToWaitBeforeTriggeringCircuitBreaker;
        readonly QueuePurger queuePurger;
        readonly PrefetchCountCalculation prefetchCountCalculation;
        readonly Action<string, Exception, CancellationToken> criticalErrorAction;
        readonly TimeSpan retryDelay;
        readonly string name;
        readonly FastConcurrentLru<string, int> deliveryAttempts = new(1_000);
        readonly FastConcurrentLru<string, bool> failedBasicAckMessages = new(1_000);

        bool disposed;
        OnMessage onMessage;
        OnError onError;
        int maxConcurrency;
        long numberOfMessagesBeingProcessed;
        CancellationTokenSource messagePumpCancellationTokenSource;
        CancellationTokenSource messageProcessingCancellationTokenSource;
        MessagePumpConnectionFailedCircuitBreaker circuitBreaker;
        IConnection connection;

        // Stop
        TaskCompletionSource<bool> connectionShutdownCompleted;

        public MessagePump(
            ReceiveSettings settings,
            ConnectionFactory connectionFactory,
            IRoutingTopology routingTopology,
            MessageConverter messageConverter,
            string consumerTag,
            ChannelProvider channelProvider,
            TimeSpan timeToWaitBeforeTriggeringCircuitBreaker,
            PrefetchCountCalculation prefetchCountCalculation,
            Action<string, Exception,
            CancellationToken> criticalErrorAction,
            TimeSpan retryDelay)
        {
            this.settings = settings;
            this.connectionFactory = connectionFactory;
            this.messageConverter = messageConverter;
            this.consumerTag = consumerTag;
            this.channelProvider = channelProvider;
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

        public Task Initialize(PushRuntimeSettings limitations, OnMessage onMessage, OnError onError, CancellationToken cancellationToken = default)
        {
            this.onMessage = onMessage;
            this.onError = onError;
            maxConcurrency = limitations.MaxConcurrency;

            if (settings.PurgeOnStartup)
            {
                queuePurger.Purge(ReceiveAddress);
            }

            return Task.CompletedTask;
        }

        public Task StartReceive(CancellationToken cancellationToken = default)
        {
            messagePumpCancellationTokenSource = new CancellationTokenSource();
            messageProcessingCancellationTokenSource = new CancellationTokenSource();

            circuitBreaker = new MessagePumpConnectionFailedCircuitBreaker(
                name,
                timeToWaitBeforeTriggeringCircuitBreaker,
                (message, exception) => criticalErrorAction(message, exception, messageProcessingCancellationTokenSource.Token));

            ConnectToBroker();

            return Task.CompletedTask;
        }

        public Task ChangeConcurrency(PushRuntimeSettings limitations, CancellationToken cancellationToken = default)
        {
            maxConcurrency = limitations.MaxConcurrency;
            Logger.InfoFormat("Calling a change concurrency and reconnecting with new value {0}.", limitations.MaxConcurrency);
            _ = Task.Run(() => Reconnect(messageProcessingCancellationTokenSource.Token), cancellationToken);
            return Task.CompletedTask;
        }

        void ConnectToBroker()
        {
            connection = connectionFactory.CreateConnection(name, false, maxConcurrency);
            connection.ConnectionShutdown += Connection_ConnectionShutdown;

            var prefetchCount = prefetchCountCalculation(maxConcurrency);

            if (prefetchCount < maxConcurrency)
            {
                Logger.Warn($"The specified prefetch count '{prefetchCount}' is smaller than the specified maximum concurrency '{maxConcurrency}'. The maximum concurrency value will be used as the prefetch count instead.");
                prefetchCount = maxConcurrency;
            }

            var channel = connection.CreateModel();
            channel.ModelShutdown += Channel_ModelShutdown;
            channel.BasicQos(0, (ushort)Math.Min(prefetchCount, ushort.MaxValue), false);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ConsumerCancelled += Consumer_ConsumerCancelled;
            consumer.Registered += Consumer_Registered;
            consumer.Received += Consumer_Received;

            channel.BasicConsume(ReceiveAddress, false, consumerTag, consumer);
        }

        public async Task StopReceive(CancellationToken cancellationToken = default)
        {
            messagePumpCancellationTokenSource?.Cancel();

            using (cancellationToken.Register(() => messageProcessingCancellationTokenSource?.Cancel()))
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

                connectionShutdownCompleted = new TaskCompletionSource<bool>();

                if (connection.IsOpen)
                {
                    connection.Close();
                }
                else
                {
                    connectionShutdownCompleted.SetResult(true);
                }

                await connectionShutdownCompleted.Task.ConfigureAwait(false);
            }
        }

#pragma warning disable PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
        Task Consumer_Registered(object sender, ConsumerEventArgs e)
#pragma warning restore PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
        {
            circuitBreaker.Success();

            return Task.CompletedTask;
        }

        void Connection_ConnectionShutdown(object sender, ShutdownEventArgs e)
        {
            if (e.Initiator == ShutdownInitiator.Application && e.ReplyCode == 200)
            {
                connectionShutdownCompleted?.TrySetResult(true);
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
        }

        void Channel_ModelShutdown(object sender, ShutdownEventArgs e)
        {
            if (e.Initiator == ShutdownInitiator.Application)
            {
                return;
            }

            if (e.Initiator == ShutdownInitiator.Peer && e.ReplyCode == 404)
            {
                return;
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
        }

#pragma warning disable PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
        Task Consumer_ConsumerCancelled(object sender, ConsumerEventArgs e)
#pragma warning restore PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
        {
            var consumer = (AsyncEventingBasicConsumer)sender;

            if (consumer.Model.IsOpen && connection.IsOpen)
            {
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
                            connection.Close();
                        }

                        connection.Dispose();

                        Logger.InfoFormat("'{0}': Attempting to reconnect in {1} seconds.", name, retryDelay.TotalSeconds);

                        await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);

                        ConnectToBroker();
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
                    consumer.Model.BasicRejectAndRequeueIfOpen(message.DeliveryTag);
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
                    consumer.Model.BasicAckSingle(message.DeliveryTag);
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
                        consumer.Model.BasicRejectAndRequeueIfOpen(message.DeliveryTag);
                        return;
                    }
                }
                catch (Exception onErrorEx) when (!onErrorEx.IsCausedBy(messageProcessingCancellationToken))
                {
                    criticalErrorAction($"Failed to execute recoverability policy for message with native ID: `{messageId}`", onErrorEx, messageProcessingCancellationToken);
                    consumer.Model.BasicRejectAndRequeueIfOpen(message.DeliveryTag);

                    return;
                }
            }

            try
            {
                consumer.Model.BasicAckSingle(message.DeliveryTag);
            }
            catch (AlreadyClosedException ex)
            {
                failedBasicAckMessages.AddOrUpdate(messageIdKey, true);

                if (Regex.IsMatch(ex.ShutdownReason.ReplyText, @"PRECONDITION_FAILED - delivery acknowledgement on channel [0-9]+ timed out\. Timeout value used: [0-9]+ ms\. This timeout value can be configured, see consumers doc guide to learn more"))
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
            if (!headers.TryGetValue(NServiceBus.Headers.DelayedRetries, out var delayedRetries))
            {
                delayedRetries = "0";
            }

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

        async Task MovePoisonMessage(AsyncEventingBasicConsumer consumer, BasicDeliverEventArgs message, string queue, CancellationToken messageProcessingCancellationToken)
        {
            try
            {
                var channel = channelProvider.GetPublishChannel();

                try
                {
                    await channel.RawSendInCaseOfFailure(queue, message.Body, message.BasicProperties, messageProcessingCancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    channelProvider.ReturnPublishChannel(channel);
                }
            }
            catch (Exception ex) when (!ex.IsCausedBy(messageProcessingCancellationToken))
            {
                Logger.Error($"Failed to move poison message to queue '{queue}'. Returning message to original queue...", ex);
                consumer.Model.BasicRejectAndRequeueIfOpen(message.DeliveryTag);

                return;
            }

            try
            {
                consumer.Model.BasicAckSingle(message.DeliveryTag);
            }
            catch (AlreadyClosedException ex)
            {
                Logger.Warn($"Failed to acknowledge poison message because the channel was closed. The message was sent to queue '{queue}' but also returned to the original queue.", ex);
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            circuitBreaker?.Dispose();
            messagePumpCancellationTokenSource?.Cancel();
            messagePumpCancellationTokenSource?.Dispose();
            // This makes sure that if the stop token hasn't been canceled the processing source is canceled
            // so that any possible reconnect attempt has the possibility to gracefully stop too.
            messageProcessingCancellationTokenSource?.Cancel();
            messageProcessingCancellationTokenSource?.Dispose();

            connection?.Dispose();
            disposed = true;
        }
    }
}