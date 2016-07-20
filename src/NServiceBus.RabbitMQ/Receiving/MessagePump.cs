namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.IO;
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
        readonly PoisonMessageForwarder poisonMessageForwarder;
        readonly QueuePurger queuePurger;
        readonly TimeSpan timeToWaitBeforeTriggeringCircuitBreaker;
        readonly FailureInfoStorage failureInfoStorage;

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

        public MessagePump(ConnectionFactory connectionFactory, MessageConverter messageConverter, string consumerTag, PoisonMessageForwarder poisonMessageForwarder, QueuePurger queuePurger, TimeSpan timeToWaitBeforeTriggeringCircuitBreaker, FailureInfoStorage failureInfoStorage)
        {
            this.connectionFactory = connectionFactory;
            this.messageConverter = messageConverter;
            this.consumerTag = consumerTag;
            this.poisonMessageForwarder = poisonMessageForwarder;
            this.queuePurger = queuePurger;
            this.timeToWaitBeforeTriggeringCircuitBreaker = timeToWaitBeforeTriggeringCircuitBreaker;
            this.failureInfoStorage = failureInfoStorage;
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
            channel.BasicQos(0, Convert.ToUInt16(limitations.MaxConcurrency), false);

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
                await ProcessMessage(eventArgs).ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
            }
        }

        async Task ProcessMessage(BasicDeliverEventArgs message)
        {
            string messageId = null;

            try
            {
                Dictionary<string, string> headers;

                try
                {
                    messageId = messageConverter.RetrieveMessageId(message);
                    headers = messageConverter.RetrieveHeaders(message);
                }
                catch (Exception ex)
                {
                    await poisonMessageForwarder.ForwardPoisonMessageToErrorQueue(message, ex, settings.ErrorQueue).ConfigureAwait(false);
                    await Acknowledge(message.DeliveryTag, messageId).ConfigureAwait(false);

                    return;
                }

                using (var tokenSource = new CancellationTokenSource())
                {
                    try
                    {
                        var messageContext = new MessageContext(messageId, headers, new MemoryStream(message.Body ?? new byte[0]), new TransportTransaction(), tokenSource, new ContextBag());
                        await onMessage(messageContext).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        failureInfoStorage.RecordFailureInfoForMessage(messageId, ex);
                        FailureInfoStorage.ProcessingFailureInfo info;
                        failureInfoStorage.TryGetFailureInfoForMessage(messageId, out info);
                        var errorContext = new ErrorContext(ex, headers, messageId, new MemoryStream(message.Body ?? new byte[0]), new TransportTransaction(), info.NumberOfProcessingAttempts);

                        if (await onError(errorContext).ConfigureAwait(false) == ErrorHandleResult.Handled)
                        {
                            failureInfoStorage.ClearFailureInfoForMessage(messageId);
                            await Acknowledge(message.DeliveryTag, messageId).ConfigureAwait(false);
                        }
                        else
                        {
                            await Requeue(message.DeliveryTag, messageId).ConfigureAwait(false);
                        }

                        return;
                    }

                    if (tokenSource.IsCancellationRequested)
                    {
                        await Requeue(message.DeliveryTag, messageId).ConfigureAwait(false);
                    }
                    else
                    {
                        await Acknowledge(message.DeliveryTag, messageId).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Error while attempting to process message {messageId}. The message will be requeued.", ex);
                await Requeue(message.DeliveryTag, messageId).ConfigureAwait(false);
            }
        }

        async Task Acknowledge(ulong deliveryTag, string messageId)
        {
            try
            {
                await consumer.Model.BasicAckSingle(deliveryTag, exclusiveScheduler).ConfigureAwait(false);
            }
            catch (AlreadyClosedException ex)
            {
                Logger.Warn($"Attempt to acknowledge message {messageId} failed because the channel was closed. The message will be requeued.", ex);
            }
        }

        async Task Requeue(ulong deliveryTag, string messageId)
        {
            try
            {
                await consumer.Model.BasicRejectAndRequeue(deliveryTag, exclusiveScheduler).ConfigureAwait(false);
            }
            catch (AlreadyClosedException)
            {
            }
        }

        public void Dispose()
        {
            // Injected
        }
    }
}
