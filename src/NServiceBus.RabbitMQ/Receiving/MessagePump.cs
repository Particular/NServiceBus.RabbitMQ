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

    class MessagePump : IPushMessages, IDisposable
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(MessagePump));

        readonly ConnectionFactory connectionFactory;
        readonly MessageConverter messageConverter;
        readonly string consumerTag;
        readonly IChannelProvider channelProvider;
        readonly QueuePurger queuePurger;
        readonly TimeSpan timeToWaitBeforeTriggeringCircuitBreaker;
        readonly ushort prefetchCountPerMessageProcessor;

        // Init
        Func<MessageContext, Task> onMessage;
        Func<ErrorContext, Task<ErrorHandleResult>> onError;
        PushSettings settings;
        MessagePumpConnectionFailedCircuitBreaker circuitBreaker;
        TaskScheduler exclusiveScheduler;

        // Start
        int maxConcurrency;
        SemaphoreSlim semaphore;
        CancellationTokenSource messageProcessing;
        IConnection connection;
        EventingBasicConsumer consumer;

        // Stop
        TaskCompletionSource<bool> connectionShutdownCompleted;

        public MessagePump(ConnectionFactory connectionFactory, MessageConverter messageConverter, string consumerTag, IChannelProvider channelProvider, QueuePurger queuePurger, TimeSpan timeToWaitBeforeTriggeringCircuitBreaker, ushort prefetchCountPerMessageProcessor)
        {
            this.connectionFactory = connectionFactory;
            this.messageConverter = messageConverter;
            this.consumerTag = consumerTag;
            this.channelProvider = channelProvider;
            this.queuePurger = queuePurger;
            this.timeToWaitBeforeTriggeringCircuitBreaker = timeToWaitBeforeTriggeringCircuitBreaker;
            this.prefetchCountPerMessageProcessor = prefetchCountPerMessageProcessor;
        }

        public Task Init(Func<MessageContext, Task> onMessage, Func<ErrorContext, Task<ErrorHandleResult>> onError, CriticalError criticalError, PushSettings settings)
        {
            this.onMessage = onMessage;
            this.onError = onError;
            this.settings = settings;

            circuitBreaker = new MessagePumpConnectionFailedCircuitBreaker($"'{settings.InputQueue} MessagePump'", timeToWaitBeforeTriggeringCircuitBreaker, criticalError);

            exclusiveScheduler = new ConcurrentExclusiveSchedulerPair().ExclusiveScheduler;

            if (settings.PurgeOnStartup)
            {
                queuePurger.Purge(settings.InputQueue);
            }

            return TaskEx.CompletedTask;
        }

        public void Start(PushRuntimeSettings limitations)
        {
            maxConcurrency = limitations.MaxConcurrency;
            semaphore = new SemaphoreSlim(limitations.MaxConcurrency, limitations.MaxConcurrency);
            messageProcessing = new CancellationTokenSource();

            connection = connectionFactory.CreateConnection($"{settings.InputQueue} MessagePump");

            var channel = connection.CreateModel();
            channel.BasicQos(0, (ushort)Math.Min(ushort.MaxValue, Convert.ToUInt16(limitations.MaxConcurrency) * prefetchCountPerMessageProcessor), false);

            consumer = new EventingBasicConsumer(channel);

            consumer.Registered += Consumer_Registered;
            connection.ConnectionShutdown += Connection_ConnectionShutdown;

            consumer.Received += Consumer_Received;

            channel.BasicConsume(settings.InputQueue, false, consumerTag, consumer);
        }

        public async Task Stop()
        {
            consumer.Received -= Consumer_Received;
            messageProcessing.Cancel();

            while (semaphore.CurrentCount != maxConcurrency)
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

        void Consumer_Registered(object sender, ConsumerEventArgs e)
        {
            circuitBreaker.Success();
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

        async void Consumer_Received(object sender, BasicDeliverEventArgs eventArgs)
        {
            try
            {
                await semaphore.WaitAsync(messageProcessing.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                await Process(eventArgs).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Warn("Failed to process message. Returning message to queue...", ex);
                await consumer.Model.BasicRejectAndRequeueIfOpen(eventArgs.DeliveryTag, exclusiveScheduler).ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
            }
        }

        async Task Process(BasicDeliverEventArgs message)
        {
            string messageId;

            try
            {
                messageId = messageConverter.RetrieveMessageId(message);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to retrieve ID from poison message. Moving message to queue '{settings.ErrorQueue}'...", ex);
                await MovePoisonMessage(message, settings.ErrorQueue).ConfigureAwait(false);

                return;
            }

            Dictionary<string, string> headers;

            try
            {
                headers = messageConverter.RetrieveHeaders(message);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to retrieve headers from poison message '{messageId}'. Moving message to queue '{settings.ErrorQueue}'...", ex);
                await MovePoisonMessage(message, settings.ErrorQueue, messageId).ConfigureAwait(false);

                return;
            }

            using (var tokenSource = new CancellationTokenSource())
            {
                var processed = false;
                var errorHandled = false;
                var numberOfDeliveryAttempts = 0;

                while (!processed && !errorHandled)
                {
                    try
                    {
                        var messageContext = new MessageContext(messageId, headers, message.Body ?? new byte[0], new TransportTransaction(), tokenSource, new ContextBag());
                        await onMessage(messageContext).ConfigureAwait(false);
                        processed = true;
                    }
                    catch (Exception ex)
                    {
                        ++numberOfDeliveryAttempts;
                        var errorContext = new ErrorContext(ex, headers, messageId, message.Body ?? new byte[0], new TransportTransaction(), numberOfDeliveryAttempts);
                        errorHandled = await onError(errorContext).ConfigureAwait(false) == ErrorHandleResult.Handled;
                    }
                }

                if (processed && tokenSource.IsCancellationRequested)
                {
                    await consumer.Model.BasicRejectAndRequeueIfOpen(message.DeliveryTag, exclusiveScheduler).ConfigureAwait(false);
                }
                else
                {
                    try
                    {
                        await consumer.Model.BasicAckSingle(message.DeliveryTag, exclusiveScheduler).ConfigureAwait(false);
                    }
                    catch (AlreadyClosedException ex)
                    {
                        Logger.Warn($"Failed to acknowledge message '{messageId}' because the channel was closed. The message was returned to the queue.", ex);
                    }
                }
            }
        }

        async Task MovePoisonMessage(BasicDeliverEventArgs message, string queue, string messageId = null)
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
                Logger.Error($"Failed to move poison message{(messageId == null ? "" : $" '{messageId}'")} to queue '{queue}'. Returning message to original queue...", ex);
                await consumer.Model.BasicRejectAndRequeueIfOpen(message.DeliveryTag, exclusiveScheduler).ConfigureAwait(false);

                return;
            }

            try
            {
                await consumer.Model.BasicAckSingle(message.DeliveryTag, exclusiveScheduler).ConfigureAwait(false);
            }
            catch (AlreadyClosedException ex)
            {
                Logger.Warn($"Failed to acknowledge poison message{(messageId == null ? "" : $" '{messageId}'")} because the channel was closed. The message was sent to queue '{queue}' but also returned to the original queue.", ex);
            }
        }

        public void Dispose()
        {
            // Injected
        }
    }
}
