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

    sealed class MessagePump : IPushMessages, IDisposable
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(MessagePump));
        static readonly TransportTransaction transportTransaction = new TransportTransaction();

        readonly ConnectionFactory connectionFactory;
        readonly MessageConverter messageConverter;
        readonly string consumerTag;
        readonly ChannelProvider channelProvider;
        readonly QueuePurger queuePurger;
        readonly TimeSpan timeToWaitBeforeTriggeringCircuitBreaker;
        readonly int prefetchMultiplier;
        readonly ushort overriddenPrefetchCount;
        readonly PushSettings settings;
        readonly Action<string, Exception> criticalError;
        readonly MessagePumpConnectionFailedCircuitBreaker circuitBreaker;
        


        // Start
        int maxConcurrency;
        long numberOfMessagesBeingProcessed;
        CancellationTokenSource messageProcessing;
        IConnection connection;
        AsyncEventingBasicConsumer consumer;
        Func<MessageContext, Task> onMessage;
        Func<ErrorContext, Task<ErrorHandleResult>> onError;

        // Stop
        TaskCompletionSource<bool> connectionShutdownCompleted;

        public MessagePump(ConnectionFactory connectionFactory, MessageConverter messageConverter, string consumerTag,
            ChannelProvider channelProvider, QueuePurger queuePurger, TimeSpan timeToWaitBeforeTriggeringCircuitBreaker,
            int prefetchMultiplier, ushort overriddenPrefetchCount, Action<string, Exception> criticalError,
            IManageSubscriptions subscriptionManager, PushSettings pushSettings)
        {
            this.connectionFactory = connectionFactory;
            this.messageConverter = messageConverter;
            this.consumerTag = consumerTag;
            this.channelProvider = channelProvider;
            this.queuePurger = queuePurger;
            this.timeToWaitBeforeTriggeringCircuitBreaker = timeToWaitBeforeTriggeringCircuitBreaker;
            this.prefetchMultiplier = prefetchMultiplier;
            this.overriddenPrefetchCount = overriddenPrefetchCount;
            this.criticalError = criticalError;
            this.settings = pushSettings;
            Subscriptions = subscriptionManager;

            circuitBreaker = new MessagePumpConnectionFailedCircuitBreaker($"'{settings.InputQueue} MessagePump'", timeToWaitBeforeTriggeringCircuitBreaker, criticalError);
        }

        public void Start(PushRuntimeSettings limitations, Func<MessageContext, Task> onMessage, Func<ErrorContext, Task<ErrorHandleResult>> onError)
        {
            this.onMessage = onMessage;
            this.onError = onError;

            if (settings.PurgeOnStartup)
            {
                queuePurger.Purge(settings.InputQueue);
            }

            maxConcurrency = limitations.MaxConcurrency;
            messageProcessing = new CancellationTokenSource();

            connection = connectionFactory.CreateConnection($"{settings.InputQueue} MessagePump", consumerDispatchConcurrency: maxConcurrency);

            var channel = connection.CreateModel();

            long prefetchCount;

            if (overriddenPrefetchCount > 0)
            {
                prefetchCount = overriddenPrefetchCount;

                if (prefetchCount < maxConcurrency)
                {
                    Logger.Warn($"The specified prefetch count '{prefetchCount}' is smaller than the specified maximum concurrency '{maxConcurrency}'. The maximum concurrency value will be used as the prefetch count instead.");
                    prefetchCount = maxConcurrency;
                }
            }
            else
            {
                prefetchCount = (long)maxConcurrency * prefetchMultiplier;
            }

            channel.BasicQos(0, (ushort)Math.Min(prefetchCount, ushort.MaxValue), false);

            consumer = new AsyncEventingBasicConsumer(channel);

            consumer.Registered += Consumer_Registered;
            connection.ConnectionShutdown += Connection_ConnectionShutdown;

            consumer.Received += Consumer_Received;

            channel.BasicConsume(settings.InputQueue, false, consumerTag, consumer);
        }

        public async Task Stop()
        {
            consumer.Received -= Consumer_Received;
            messageProcessing.Cancel();

            while (Interlocked.Read(ref numberOfMessagesBeingProcessed) > 0)
            {
                await Task.Delay(50).ConfigureAwait(false);
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

        public IManageSubscriptions Subscriptions { get; }

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
            if (messageProcessing.Token.IsCancellationRequested)
            {
                return;
            }

            Interlocked.Increment(ref numberOfMessagesBeingProcessed);

            try
            {
                await Process(eventArgs).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Warn("Failed to process message. Returning message to queue...", ex);
                await consumer.Model.BasicRejectAndRequeueIfOpen(eventArgs.DeliveryTag).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Decrement(ref numberOfMessagesBeingProcessed);
            }
        }

        async Task Process(BasicDeliverEventArgs message)
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

            using (var tokenSource = new CancellationTokenSource())
            {
                var processed = false;
                var errorHandled = false;
                var numberOfDeliveryAttempts = 0;
                var messageBody = message.Body.ToArray();

                while (!processed && !errorHandled)
                {
                    try
                    {
                        var contextBag = new ContextBag();
                        contextBag.Set(message);

                        var messageContext = new MessageContext(messageId, headers, messageBody ?? Array.Empty<byte>(), transportTransaction, tokenSource, contextBag);

                        await onMessage(messageContext).ConfigureAwait(false);
                        processed = true;
                    }
                    catch (Exception exception)
                    {
                        ++numberOfDeliveryAttempts;
                        headers = messageConverter.RetrieveHeaders(message);
                        var contextBag = new ContextBag();
                        contextBag.Set(message);

                        var errorContext = new ErrorContext(exception, headers, messageId, messageBody ?? Array.Empty<byte>(), transportTransaction, numberOfDeliveryAttempts, contextBag);

                        try
                        {
                            errorHandled = await onError(errorContext).ConfigureAwait(false) == ErrorHandleResult.Handled;

                            if (!errorHandled)
                            {
                                headers = messageConverter.RetrieveHeaders(message);
                            }
                        }
                        catch (Exception ex)
                        {
                            criticalError($"Failed to execute recoverability policy for message with native ID: `{messageId}`", ex);
                            await consumer.Model.BasicRejectAndRequeueIfOpen(message.DeliveryTag).ConfigureAwait(false);

                            return;
                        }
                    }
                }

                if (processed && tokenSource.IsCancellationRequested)
                {
                    await consumer.Model.BasicRejectAndRequeueIfOpen(message.DeliveryTag).ConfigureAwait(false);
                }
                else
                {
                    try
                    {
                        await consumer.Model.BasicAckSingle(message.DeliveryTag).ConfigureAwait(false);
                    }
                    catch (AlreadyClosedException ex)
                    {
                        Logger.Warn($"Failed to acknowledge message '{messageId}' because the channel was closed. The message was returned to the queue.", ex);
                    }
                }
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
                await consumer.Model.BasicRejectAndRequeueIfOpen(message.DeliveryTag).ConfigureAwait(false);

                return;
            }

            try
            {
                await consumer.Model.BasicAckSingle(message.DeliveryTag).ConfigureAwait(false);
            }
            catch (AlreadyClosedException ex)
            {
                Logger.Warn($"Failed to acknowledge poison message because the channel was closed. The message was sent to queue '{queue}' but also returned to the original queue.", ex);
            }
        }

        public void Dispose()
        {
            circuitBreaker?.Dispose();
            messageProcessing?.Dispose();
            connection?.Dispose();
        }
    }
}
