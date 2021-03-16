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

            connection = connectionFactory.CreateConnection($"{settings.ReceiveAddress} MessagePump", consumerDispatchConcurrency: maxConcurrency);

            var channel = connection.CreateModel();

            var prefetchCount = prefetchCountCalculation(maxConcurrency);

            if (prefetchCount < maxConcurrency)
            {
                Logger.Warn($"The specified prefetch count '{prefetchCount}' is smaller than the specified maximum concurrency '{maxConcurrency}'. The maximum concurrency value will be used as the prefetch count instead.");
                prefetchCount = maxConcurrency;
            }

            channel.BasicQos(0, (ushort)Math.Min(prefetchCount, ushort.MaxValue), false);

            consumer = new AsyncEventingBasicConsumer(channel);

            consumer.Registered += Consumer_Registered;
            connection.ConnectionShutdown += Connection_ConnectionShutdown;

            consumer.Received += Consumer_Received;

            channel.BasicConsume(settings.ReceiveAddress, false, consumerTag, consumer);

            return Task.CompletedTask;
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
                    // We do not want to rudely abort them because the cancellation token has been cancelled.
                    // This allows us to preserve the same behaviour in v8 as in v7 in that,
                    // if CancellationToken.None is passed to this method,
                    // the method will only return when all in flight messages have been processed.
                    // If, on the other hand, a non-default CancellationToken is passed,
                    // all message processing operations have the opportunity to
                    // participate in cooperative cancellation.
                    // If we ever require a method of stopping the endpoint such that
                    // all message processing is cancelled immediately,
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

        Task Consumer_Registered(object sender, ConsumerEventArgs e)
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
            }
        }

        async Task Consumer_Received(object sender, BasicDeliverEventArgs eventArgs)
        {
            if (messagePumpCancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            Interlocked.Increment(ref numberOfMessagesBeingProcessed);

            try
            {
                await Process(eventArgs, messageProcessingCancellationTokenSource.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Warn("Failed to process message. Returning message to queue...", ex);
                consumer.Model.BasicRejectAndRequeueIfOpen(eventArgs.DeliveryTag);
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
                Logger.Error($"Failed to retrieve headers from poison message. Moving message to queue '{settings.ErrorQueue}'...", ex);
                await MovePoisonMessage(message, settings.ErrorQueue).ConfigureAwait(false);

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
                await MovePoisonMessage(message, settings.ErrorQueue).ConfigureAwait(false);

                return;
            }

            var processed = false;
            var errorHandled = false;
            var numberOfDeliveryAttempts = 0;
            var messageBody = message.Body.ToArray();

            while (!processed && !errorHandled)
            {
                var processingContext = new ContextBag();

                processingContext.Set(message);

                try
                {

                    var messageContext = new MessageContext(messageId, headers, messageBody ?? Array.Empty<byte>(), TransportTransaction, processingContext);

                    await onMessage(messageContext, messageProcessingCancellationToken).ConfigureAwait(false);
                    processed = true;
                }
                catch (OperationCanceledException) when (messageProcessingCancellationToken.IsCancellationRequested)
                {
                    consumer.Model.BasicRejectAndRequeueIfOpen(message.DeliveryTag);

                    return;
                }
                catch (Exception exception)
                {
                    ++numberOfDeliveryAttempts;
                    headers = messageConverter.RetrieveHeaders(message);

                    var errorContext = new ErrorContext(exception, headers, messageId, messageBody ?? Array.Empty<byte>(), TransportTransaction, numberOfDeliveryAttempts, processingContext);

                    try
                    {
                        errorHandled = await onError(errorContext, messageProcessingCancellationToken).ConfigureAwait(false) == ErrorHandleResult.Handled;

                        if (!errorHandled)
                        {
                            headers = messageConverter.RetrieveHeaders(message);
                        }
                    }
                    catch (OperationCanceledException) when (messageProcessingCancellationToken.IsCancellationRequested)
                    {
                        consumer.Model.BasicRejectAndRequeueIfOpen(message.DeliveryTag);

                        return;
                    }
                    catch (Exception ex)
                    {
                        criticalErrorAction($"Failed to execute recoverability policy for message with native ID: `{messageId}`", ex, messageProcessingCancellationToken);
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
                Logger.Warn($"Failed to acknowledge message '{messageId}' because the channel was closed. The message was returned to the queue.", ex);
            }
        }

        async Task MovePoisonMessage(BasicDeliverEventArgs message, string queue)
        {
            try
            {
                var channel = channelProvider.GetPublishChannel();

                try
                {
                    await channel.RawSendInCaseOfFailure(queue, message.Body, message.BasicProperties).ConfigureAwait(false);
                }
                finally
                {
                    channelProvider.ReturnPublishChannel(channel);
                }
            }
            catch (Exception ex)
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
