namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using global::RabbitMQ.Client.Exceptions;
    using Logging;

    sealed class MessagePump : IMessageReceiver, IDisposable
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(MessagePump));
        static readonly TransportTransaction TransportTransaction = new TransportTransaction();

        readonly ConnectionFactory connectionFactory;
        readonly MessageConverter messageConverter;
        readonly string consumerTag;
        readonly ChannelProvider channelProvider;
        readonly TimeSpan timeToWaitBeforeTriggeringCircuitBreaker;
        readonly QueuePurger queuePurger;
        readonly PrefetchCountCalculation prefetchCountCalculation;
        readonly ReceiveSettings settings;
        readonly Action<string, Exception, CancellationToken> criticalErrorAction;

        bool disposed;
        OnMessage onMessage;
        OnError onError;
        int maxConcurrency;
        long numberOfMessagesBeingProcessed;
        CancellationTokenSource messagePumpCancellationTokenSource;
        CancellationTokenSource messageProcessingCancellationTokenSource;
        MessagePumpConnectionFailedCircuitBreaker circuitBreaker;
        IConnection connection;
        AsyncEventingBasicConsumer consumer;

        // Stop
        TaskCompletionSource<bool> connectionShutdownCompleted;

        public MessagePump(ConnectionFactory connectionFactory, IRoutingTopology routingTopology, MessageConverter messageConverter, string consumerTag,
            ChannelProvider channelProvider, TimeSpan timeToWaitBeforeTriggeringCircuitBreaker,
            PrefetchCountCalculation prefetchCountCalculation, ReceiveSettings settings,
            Action<string, Exception, CancellationToken> criticalErrorAction)
        {
            this.connectionFactory = connectionFactory;
            this.messageConverter = messageConverter;
            this.consumerTag = consumerTag;
            this.channelProvider = channelProvider;
            this.timeToWaitBeforeTriggeringCircuitBreaker = timeToWaitBeforeTriggeringCircuitBreaker;
            this.prefetchCountCalculation = prefetchCountCalculation;
            this.settings = settings;
            this.criticalErrorAction = criticalErrorAction;

            if (settings.UsePublishSubscribe)
            {
                Subscriptions = new SubscriptionManager(connectionFactory, routingTopology, settings.ReceiveAddress);
            }

            queuePurger = new QueuePurger(connectionFactory);
        }

        public ISubscriptionManager Subscriptions { get; }
        public string Id => settings.Id;

        public Task Initialize(PushRuntimeSettings limitations, OnMessage onMessage, OnError onError, CancellationToken cancellationToken = default)
        {
            this.onMessage = onMessage;
            this.onError = onError;
            maxConcurrency = limitations.MaxConcurrency;

            if (settings.PurgeOnStartup)
            {
                queuePurger.Purge(settings.ReceiveAddress);
            }

            return Task.CompletedTask;
        }

        public Task StartReceive(CancellationToken cancellationToken = default)
        {
            messagePumpCancellationTokenSource = new CancellationTokenSource();
            messageProcessingCancellationTokenSource = new CancellationTokenSource();

            circuitBreaker = new MessagePumpConnectionFailedCircuitBreaker(
                $"'{settings.ReceiveAddress} MessagePump'",
                timeToWaitBeforeTriggeringCircuitBreaker,
                (message, exception) => criticalErrorAction(message, exception, messageProcessingCancellationTokenSource.Token));

            ConnectToBroker();

            return Task.CompletedTask;
        }

        void ConnectToBroker()
        {
            connection = connectionFactory.CreateConnection($"{settings.ReceiveAddress} MessagePump", false, maxConcurrency);
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

            consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ConsumerCancelled += Consumer_ConsumerCancelled;
            consumer.Registered += Consumer_Registered;
            consumer.Received += Consumer_Received;

            channel.BasicConsume(settings.ReceiveAddress, false, consumerTag, consumer);
        }

        public async Task StopReceive(CancellationToken cancellationToken = default)
        {
            consumer.Received -= Consumer_Received;

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
            else
            {
                circuitBreaker.Failure(new Exception(e.ToString()));
                _ = Task.Run(() => Reconnect());
            }
        }

        void Channel_ModelShutdown(object sender, ShutdownEventArgs e)
        {
            if (e.Initiator != ShutdownInitiator.Application)
            {
                circuitBreaker.Failure(new Exception(e.ToString()));
                _ = Task.Run(() => Reconnect());
            }
        }

#pragma warning disable PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
        Task Consumer_ConsumerCancelled(object sender, ConsumerEventArgs e)
#pragma warning restore PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
        {
            if (consumer.Model.IsOpen && connection.IsOpen)
            {
                circuitBreaker.Failure(new Exception("TODO: Consumer cancelled message"));
                _ = Task.Run(() => Reconnect());
            }

            return Task.CompletedTask;
        }

#pragma warning disable PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
        async Task Reconnect()
#pragma warning restore PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
        {
            var oldConsumer = consumer;
            var oldChannel = consumer.Model;
            var oldConnection = connection;

            var retryDelay = TimeSpan.FromSeconds(10); // replace with passed in value

            Logger.InfoFormat("Attempting to reconnect in {0} seconds.", retryDelay.TotalSeconds);

            while (true)
            {
                await Task.Delay(retryDelay, CancellationToken.None).ConfigureAwait(false);

                try
                {
                    ConnectToBroker();
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Info("Reconnecting to the broker failed.", ex);
                }
            }

            Logger.Info("Connection to the broker reestablished successfully.");

            if (oldChannel.IsOpen)
            {
                oldChannel.Close();
                oldChannel.Dispose();
            }

            if (oldConnection.IsOpen)
            {
                oldConnection.Close();
                oldConnection.Dispose();
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

            await ProcessAndSwallowExceptions(eventArgs, messageProcessingCancellationTokenSource.Token).ConfigureAwait(false);
        }

        async Task ProcessAndSwallowExceptions(BasicDeliverEventArgs message, CancellationToken messageProcessingCancellationToken)
        {
            Interlocked.Increment(ref numberOfMessagesBeingProcessed);

            try
            {
                try
                {
                    await Process(message, messageProcessingCancellationToken).ConfigureAwait(false);
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

        async Task Process(BasicDeliverEventArgs message, CancellationToken messageProcessingCancellationToken)
        {
            Dictionary<string, string> headers;

            try
            {
                headers = messageConverter.RetrieveHeaders(message);
            }
            catch (Exception ex)
            {
                Logger.Error(
                    $"Failed to retrieve headers from poison message. Moving message to queue '{settings.ErrorQueue}'...",
                    ex);
                await MovePoisonMessage(message, settings.ErrorQueue, messageProcessingCancellationToken).ConfigureAwait(false);

                return;
            }

            string messageId;

            try
            {
                messageId = messageConverter.RetrieveMessageId(message, headers);
            }
            catch (Exception ex)
            {
                Logger.Error(
                    $"Failed to retrieve ID from poison message. Moving message to queue '{settings.ErrorQueue}'...",
                    ex);
                await MovePoisonMessage(message, settings.ErrorQueue, messageProcessingCancellationToken).ConfigureAwait(false);

                return;
            }

            var processed = false;
            var errorHandled = false;
            var numberOfDeliveryAttempts = 0;

            while (!processed && !errorHandled)
            {
                var processingContext = new ContextBag();

                processingContext.Set(message);

                try
                {

                    var messageContext = new MessageContext(messageId, headers, message.Body, TransportTransaction, processingContext);

                    await onMessage(messageContext, messageProcessingCancellationToken).ConfigureAwait(false);
                    processed = true;
                }
                catch (Exception ex) when (!ex.IsCausedBy(messageProcessingCancellationToken))
                {
                    ++numberOfDeliveryAttempts;
                    headers = messageConverter.RetrieveHeaders(message);

                    var errorContext = new ErrorContext(ex, headers, messageId, message.Body, TransportTransaction, numberOfDeliveryAttempts, processingContext);

                    try
                    {
                        errorHandled =
                            await onError(errorContext, messageProcessingCancellationToken).ConfigureAwait(false) ==
                            ErrorHandleResult.Handled;

                        if (!errorHandled)
                        {
                            headers = messageConverter.RetrieveHeaders(message);
                        }
                    }
                    catch (Exception onErrorEx) when (!onErrorEx.IsCausedBy(messageProcessingCancellationToken))
                    {
                        criticalErrorAction(
                            $"Failed to execute recoverability policy for message with native ID: `{messageId}`", onErrorEx,
                            messageProcessingCancellationToken);
                        consumer.Model.BasicRejectAndRequeueIfOpen(message.DeliveryTag);

                        return;
                    }
                }
            }

            try
            {
                consumer.Model.BasicAckSingle(message.DeliveryTag);
            }
            catch (AlreadyClosedException ex)
            {
                Logger.Warn(
                    $"Failed to acknowledge message '{messageId}' because the channel was closed. The message was returned to the queue.",
                    ex);
            }
        }

        async Task MovePoisonMessage(BasicDeliverEventArgs message, string queue, CancellationToken messageProcessingCancellationToken)
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
            messagePumpCancellationTokenSource?.Dispose();
            messageProcessingCancellationTokenSource?.Dispose();

            connection?.Dispose();
            disposed = true;
        }
    }
}
