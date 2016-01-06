namespace NServiceBus.Transports.RabbitMQ
{
    using Extensibility;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using NServiceBus.Transports.RabbitMQ.Config;
    using NServiceBus.Transports.RabbitMQ.Connection;

    class MessagePump : IPushMessages, IDisposable
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(MessagePump));

        readonly ReceiveOptions receiveOptions;
        readonly ConnectionConfiguration connectionConfiguration;
        readonly PoisonMessageForwarder poisonMessageForwarder;
        readonly QueuePurger queuePurger;

        RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;
        Func<PushContext, Task> pipe;
        PushSettings settings;
        SecondaryReceiveSettings secondaryReceiveSettings;
        bool noAck;

        PersistentConnection connection;

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

            // TODO: Read these from config
            var timeToWaitBeforeTriggering = TimeSpan.FromMinutes(2);
            var delayAfterFailure = TimeSpan.FromSeconds(5);

            circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("RabbitMqConnectivity",
                timeToWaitBeforeTriggering,
                ex => criticalError.Raise("Repeated failures when communicating with the broker",
                ex), delayAfterFailure);

            secondaryReceiveSettings = receiveOptions.GetSettings(settings.InputQueue);
            noAck = settings.RequiredTransactionMode == TransportTransactionMode.None;

            if (settings.PurgeOnStartup)
            {
                queuePurger.Purge(settings.InputQueue);
            }

            return TaskEx.Completed;
        }

        ConcurrentExclusiveSchedulerPair taskScheduler;
        CancellationTokenSource cancelSource;

        public void Start(PushRuntimeSettings limitations)
        {
            executingCounter = 0;
            taskScheduler = new ConcurrentExclusiveSchedulerPair(TaskScheduler.Default, limitations.MaxConcurrency);
            var factory = new RabbitMqConnectionFactory(connectionConfiguration, taskScheduler.ConcurrentScheduler);
            connection = new PersistentConnection(factory, connectionConfiguration.RetryDelay, "Consume");
            cancelSource = new CancellationTokenSource();
            var model = connection.CreateModel();
            model.BasicQos(0, Convert.ToUInt16(limitations.MaxConcurrency), false);

            var consumer = new EventingBasicConsumer(model);

            cancelSource.Token.Register(() => consumer.Received -= ConsumerOnReceived);

            consumer.Received += ConsumerOnReceived;

            model.BasicConsume(settings.InputQueue, noAck, consumer);

            if (secondaryReceiveSettings.IsEnabled)
            {
                model.BasicConsume(secondaryReceiveSettings.ReceiveQueue, noAck, consumer);
            }
        }

        int executingCounter;

        public async Task Stop()
        {
            cancelSource.Cancel();

            while (Interlocked.CompareExchange(ref executingCounter, 0, 0) != 0)
            {
                await Task.Yield();
            }

            connection.Close();

            cancelSource.Dispose();
        }

        async void ConsumerOnReceived(object sender, BasicDeliverEventArgs eventArgs)
        {
            try
            {
                Interlocked.Increment(ref executingCounter);
                circuitBreaker.Success();
                var originalConsumer = (EventingBasicConsumer)sender;
                await ProcessMessage(eventArgs, originalConsumer.Model, taskScheduler.ExclusiveScheduler).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Decrement(ref executingCounter);
            }
        }

        async Task ProcessMessage(BasicDeliverEventArgs message, IModel channel, TaskScheduler scheduler)
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

                if (pushMessage)
                {
                    await PushMessageToPipe(messageId, headers, new MemoryStream(message.Body ?? new byte[0])).ConfigureAwait(false);
                }

                if (!noAck)
                {
                    var task = new Task(() => { channel.BasicAck(message.DeliveryTag, false); });
                    task.Start(scheduler);
                    await task.ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                if (!noAck)
                {
                    var task = new Task(() => { channel.BasicReject(message.DeliveryTag, true); });
                    task.Start(scheduler);
                    await task.ConfigureAwait(false);
                }
            }
        }

        async Task PushMessageToPipe(string messageId, Dictionary<string, string> headers, Stream stream)
        {
            var contextBag = new ContextBag();

            string explicitCallbackAddress;

            if (headers.TryGetValue(Callbacks.HeaderKey, out explicitCallbackAddress))
            {
                contextBag.Set(new CallbackAddress(explicitCallbackAddress));
            }

            var pushContext = new PushContext(messageId, headers, stream, new TransportTransaction(), contextBag);

            await Task.Run(() => pipe(pushContext)).ConfigureAwait(false);
        }

        public void Dispose()
        {
            // Injected
        }
    }
}
