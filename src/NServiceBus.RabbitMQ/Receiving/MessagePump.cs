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
    using Transports;

    class MessagePump : IPushMessages, IDisposable
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(MessagePump));

        readonly ConnectionFactory connectionFactory;
        readonly MessageConverter messageConverter;
        readonly string consumerTag;
        readonly PoisonMessageForwarder poisonMessageForwarder;
        readonly QueuePurger queuePurger;
        readonly TimeSpan timeToWaitBeforeTriggeringCircuitBreaker;

        Func<PushContext, Task> pipe;
        PushSettings settings;
        MessagePumpConnectionFailedCircuitBreaker circuitBreaker;
        TaskScheduler exclusiveScheduler;

        int maxConcurrency;
        SemaphoreSlim semaphore;
        CancellationTokenSource messageProcessing;
        IConnection connection;
        EventingBasicConsumer consumer;
        TaskCompletionSource<bool> connectionShutdownCompleted;

        public MessagePump(ConnectionFactory connectionFactory, MessageConverter messageConverter, string consumerTag, PoisonMessageForwarder poisonMessageForwarder, QueuePurger queuePurger, TimeSpan timeToWaitBeforeTriggeringCircuitBreaker)
        {
            this.connectionFactory = connectionFactory;
            this.messageConverter = messageConverter;
            this.consumerTag = consumerTag;
            this.poisonMessageForwarder = poisonMessageForwarder;
            this.queuePurger = queuePurger;
            this.timeToWaitBeforeTriggeringCircuitBreaker = timeToWaitBeforeTriggeringCircuitBreaker;
        }

        public Task Init(Func<PushContext, Task> pipe, CriticalError criticalError, PushSettings settings)
        {
            this.pipe = pipe;
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
            await ProcessMessage(eventArgs, consumer.Model).ConfigureAwait(false);
        }

        async Task ProcessMessage(BasicDeliverEventArgs message, IModel channel)
        {
            try
            {
                await semaphore.WaitAsync(messageProcessing.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            Dictionary<string, string> headers = null;
            string messageId = null;
            var pushMessage = false;

            try
            {
                try
                {
                    messageId = messageConverter.RetrieveMessageId(message);
                    headers = messageConverter.RetrieveHeaders(message);

                    pushMessage = true;
                }
                catch (Exception ex)
                {
                    await poisonMessageForwarder.ForwardPoisonMessageToErrorQueue(message, ex, settings.ErrorQueue).ConfigureAwait(false);
                }

                var rejectMessage = false;

                if (pushMessage)
                {
                    using (var tokenSource = new CancellationTokenSource())
                    {
                        var pushContext = new PushContext(messageId, headers, new MemoryStream(message.Body ?? new byte[0]), new TransportTransaction(), tokenSource, new ContextBag());

                        await pipe(pushContext).ConfigureAwait(false);

                        rejectMessage = tokenSource.IsCancellationRequested;
                    }
                }

                if (rejectMessage)
                {
                    try
                    {
                        await channel.RejectMessage(message.DeliveryTag, exclusiveScheduler).ConfigureAwait(false);
                    }
                    catch (AlreadyClosedException ex)
                    {
                        Logger.Warn($"Attempt to reject message {messageId} failed because the channel was closed. The message will be requeued.", ex);
                    }
                }
                else
                {
                    try
                    {
                        await channel.AcknowledgeMessage(message.DeliveryTag, exclusiveScheduler).ConfigureAwait(false);
                    }
                    catch (AlreadyClosedException ex)
                    {
                        Logger.Warn($"Attempt to acknowledge message {messageId} failed because the channel was closed. The message will be requeued.", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Error while attempting to process message {messageId}. The message will be rejected.", ex);

                try
                {
                    await channel.RejectMessage(message.DeliveryTag, exclusiveScheduler).ConfigureAwait(false);
                }
                catch (AlreadyClosedException ex2)
                {
                    Logger.Warn($"Attempt to reject message {messageId} failed because the channel was closed. The message will be requeued.", ex2);
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        public void Dispose()
        {
            // Injected
        }
    }
}
