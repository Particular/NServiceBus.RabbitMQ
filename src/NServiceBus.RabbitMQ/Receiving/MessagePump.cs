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

        readonly ConnectionConfiguration connectionConfiguration;
        readonly MessageConverter messageConverter;
        readonly string consumerTag;
        readonly PoisonMessageForwarder poisonMessageForwarder;
        readonly QueuePurger queuePurger;
        readonly TimeSpan timeToWaitBeforeTriggeringCircuitBreaker;

        Func<PushContext, Task> pipe;
        PushSettings settings;
        MessagePumpConnectionFailedCircuitBreaker circuitBreaker;

        int maxConcurrency;
        SemaphoreSlim semaphore;
        CancellationTokenSource messageProcessing;
        ConcurrentExclusiveSchedulerPair taskScheduler;
        IConnection connection;
        EventingBasicConsumer consumer;
        TaskCompletionSource<bool> connectionShutdownCompleted;

        public MessagePump(ConnectionConfiguration connectionConfiguration, MessageConverter messageConverter, string consumerTag, PoisonMessageForwarder poisonMessageForwarder, QueuePurger queuePurger, TimeSpan timeToWaitBeforeTriggeringCircuitBreaker)
        {
            this.connectionConfiguration = connectionConfiguration;
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

            circuitBreaker = new MessagePumpConnectionFailedCircuitBreaker($"'{settings.InputQueue} MessagePump'",
                timeToWaitBeforeTriggeringCircuitBreaker,
                ex => criticalError.Raise($"{settings.InputQueue} MessagePump's connection to the broker has failed.", ex));

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

            taskScheduler = new ConcurrentExclusiveSchedulerPair(TaskScheduler.Default, limitations.MaxConcurrency);
            var factory = new ConnectionFactory(connectionConfiguration, taskScheduler.ConcurrentScheduler);
            connection = factory.CreateConnection($"{settings.InputQueue} MessagePump");

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
            await ProcessMessage(eventArgs, consumer.Model).ConfigureAwait(true);
        }

        async Task ProcessMessage(BasicDeliverEventArgs message, IModel channel)
        {
            try
            {
                await semaphore.WaitAsync(messageProcessing.Token).ConfigureAwait(true);
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
                    await poisonMessageForwarder.ForwardPoisonMessageToErrorQueue(message, ex, settings.ErrorQueue).ConfigureAwait(true);
                }

                CancellationTokenSource tokenSource = null;

                if (pushMessage)
                {
                    tokenSource = new CancellationTokenSource();
                    await PushMessageToPipe(messageId, headers, tokenSource, new MemoryStream(message.Body ?? new byte[0])).ConfigureAwait(true);
                }

                var cancellationRequested = tokenSource?.IsCancellationRequested ?? false;

                if (cancellationRequested)
                {
                    await RejectMessage(channel, message.DeliveryTag, messageId).ConfigureAwait(true);
                }
                else
                {
                    await AcknowledgeMessage(channel, message.DeliveryTag, messageId).ConfigureAwait(true);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Error while attempting to process message {messageId}. The message will be rejected.", ex);

                await RejectMessage(channel, message.DeliveryTag, messageId).ConfigureAwait(true);
            }
            finally
            {
                semaphore.Release();
            }
        }

        Task PushMessageToPipe(string messageId, Dictionary<string, string> headers, CancellationTokenSource tokenSource, Stream stream)
        {
            var contextBag = new ContextBag();
            var pushContext = new PushContext(messageId, headers, stream, new TransportTransaction(), tokenSource, contextBag);

            return Task.Run(() => pipe(pushContext));
        }

        async Task AcknowledgeMessage(IModel channel, ulong deliveryTag, string messageId)
        {
            try
            {
                var task = new Task(() =>
                {
                    if (channel.IsOpen)
                    {
                        channel.BasicAck(deliveryTag, false);
                    }
                    else
                    {
                        Logger.Warn($"Attempt to acknowledge message {messageId} failed because the channel was closed. The message will be requeued.");
                    }
                });

                task.Start(taskScheduler.ExclusiveScheduler);
                await task.ConfigureAwait(true);
            }
            catch (AlreadyClosedException ex)
            {
                Logger.Warn($"Attempt to acknowledge message {messageId} failed because the channel was closed. The message will be requeued.", ex);
            }
        }

        async Task RejectMessage(IModel channel, ulong deliveryTag, string messageId)
        {
            try
            {
                var task = new Task(() =>
                {
                    if (channel.IsOpen)
                    {
                        channel.BasicReject(deliveryTag, true);
                    }
                    else
                    {
                        Logger.Warn($"Attempt to reject message {messageId} failed because the channel was closed. The message will be requeued.");
                    }
                });

                task.Start(taskScheduler.ExclusiveScheduler);
                await task.ConfigureAwait(true);

            }
            catch (AlreadyClosedException ex)
            {
                Logger.Warn($"Attempt to reject message {messageId} failed because the channel was closed. The message will be requeued.", ex);
            }
        }

        public void Dispose()
        {
            // Injected
        }
    }
}
