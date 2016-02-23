namespace NServiceBus.Transports.RabbitMQ
{
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using global::RabbitMQ.Client.Exceptions;
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using NServiceBus.Transports.RabbitMQ.Config;
    using NServiceBus.Transports.RabbitMQ.Connection;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    class MessagePump : IPushMessages, IDisposable
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(MessagePump));

        readonly ReceiveOptions receiveOptions;
        readonly ConnectionConfiguration connectionConfiguration;
        readonly PoisonMessageForwarder poisonMessageForwarder;
        readonly QueuePurger queuePurger;

        MessagePumpConnectionFailedCircuitBreaker circuitBreaker;
        Func<PushContext, Task> pipe;
        PushSettings settings;

        ConcurrentDictionary<int, Task> inFlightMessages;
        ConcurrentExclusiveSchedulerPair taskScheduler;
        IConnection connection;
        EventingBasicConsumer consumer;
        TaskCompletionSource<bool> connectionShutdownCompleted;

        public MessagePump(ReceiveOptions receiveOptions, ConnectionConfiguration connectionConfiguration, PoisonMessageForwarder poisonMessageForwarder, QueuePurger queuePurger)
        {
            this.receiveOptions = receiveOptions;
            this.connectionConfiguration = connectionConfiguration;
            this.poisonMessageForwarder = poisonMessageForwarder;
            this.queuePurger = queuePurger;
        }

        public Task Init(Func<PushContext, Task> pipe, CriticalError criticalError, PushSettings settings)
        {
            this.pipe = pipe;
            this.settings = settings;

            // TODO: Read from config and deprecate delayAfterFailure
            var timeToWaitBeforeTriggering = TimeSpan.FromMinutes(2);

            circuitBreaker = new MessagePumpConnectionFailedCircuitBreaker($"'{settings.InputQueue} MessagePump'",
                timeToWaitBeforeTriggering,
                ex => criticalError.Raise($"{settings.InputQueue} MessagePump's connection to the broker has failed.", ex));

            if (settings.PurgeOnStartup)
            {
                queuePurger.Purge(settings.InputQueue);
            }

            return TaskEx.Completed;
        }

        public void Start(PushRuntimeSettings limitations)
        {
            inFlightMessages = new ConcurrentDictionary<int, Task>(limitations.MaxConcurrency, limitations.MaxConcurrency);

            taskScheduler = new ConcurrentExclusiveSchedulerPair(TaskScheduler.Default, limitations.MaxConcurrency);
            var factory = new RabbitMqConnectionFactory(connectionConfiguration, taskScheduler.ConcurrentScheduler);
            connection = factory.CreateConnection($"{settings.InputQueue} MessagePump");

            var model = connection.CreateModel();
            model.BasicQos(0, Convert.ToUInt16(limitations.MaxConcurrency), false);

            consumer = new EventingBasicConsumer(model);

            consumer.Registered += Consumer_Registered;
            connection.ConnectionShutdown += Connection_ConnectionShutdown;

            consumer.Received += Consumer_Received;

            model.BasicConsume(settings.InputQueue, false, receiveOptions.ConsumerTag, consumer);
        }

        public async Task Stop()
        {
            consumer.Received -= Consumer_Received;

            await Task.WhenAll(inFlightMessages.Values).ConfigureAwait(false);

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
                circuitBreaker.Failure(new Exception());
            }
        }

        async void Consumer_Received(object sender, BasicDeliverEventArgs eventArgs)
        {
            Task task = null;

            try
            {
                var consumer = (EventingBasicConsumer)sender;
                task = ProcessMessage(eventArgs, consumer.Model);
                inFlightMessages.TryAdd(task.Id, task);

                await task.ConfigureAwait(false);
            }
            finally
            {
                inFlightMessages.TryRemove(task.Id, out task);
            }
        }

        async Task ProcessMessage(BasicDeliverEventArgs message, IModel channel)
        {
            Dictionary<string, string> headers = null;
            string messageId = null;
            var pushMessage = false;

            try
            {
                try
                {
                    messageId = receiveOptions.Converter.RetrieveMessageId(message);
                    headers = receiveOptions.Converter.RetrieveHeaders(message);
                    pushMessage = true;
                }
                catch (Exception ex)
                {
                    poisonMessageForwarder.ForwardPoisonMessageToErrorQueue(message, ex, settings.ErrorQueue);
                }

                CancellationTokenSource tokenSource = null;

                if (pushMessage)
                {
                    tokenSource = new CancellationTokenSource();
                    await PushMessageToPipe(messageId, headers, tokenSource, new MemoryStream(message.Body ?? new byte[0])).ConfigureAwait(false);
                }

                var cancellationRequested = tokenSource?.IsCancellationRequested ?? false;

                if (cancellationRequested)
                {
                    await RejectMessage(channel, message.DeliveryTag, messageId).ConfigureAwait(false);
                }
                else
                {
                    await AcknowledgeMessage(channel, message.DeliveryTag, messageId).ConfigureAwait(false);
                }
            }
            catch
            {
                await RejectMessage(channel, message.DeliveryTag, messageId).ConfigureAwait(false);
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
                await task.ConfigureAwait(false);
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
                await task.ConfigureAwait(false);

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
