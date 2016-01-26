namespace NServiceBus.Transports.RabbitMQ
{
    using Extensibility;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
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
        readonly ReceiveOptions receiveOptions;
        readonly ConnectionConfiguration connectionConfiguration;
        readonly PoisonMessageForwarder poisonMessageForwarder;
        readonly QueuePurger queuePurger;

        RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;
        Func<PushContext, Task> pipe;
        PushSettings settings;
        SecondaryReceiveSettings secondaryReceiveSettings;

        ConcurrentDictionary<int, Task> inFlightMessages;
        IConnection connection;
        EventingBasicConsumer consumer;
        TaskCompletionSource<bool> consumerShutdownCompleted;

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

            if (settings.PurgeOnStartup)
            {
                queuePurger.Purge(settings.InputQueue);
            }

            return TaskEx.Completed;
        }

        ConcurrentExclusiveSchedulerPair taskScheduler;

        public void Start(PushRuntimeSettings limitations)
        {
            inFlightMessages = new ConcurrentDictionary<int, Task>(limitations.MaxConcurrency, limitations.MaxConcurrency);

            taskScheduler = new ConcurrentExclusiveSchedulerPair(TaskScheduler.Default, limitations.MaxConcurrency);
            var factory = new RabbitMqConnectionFactory(connectionConfiguration, taskScheduler.ConcurrentScheduler);
            connection = factory.CreateConnection($"{settings.InputQueue} MessagePump");

            var model = connection.CreateModel();
            model.BasicQos(0, Convert.ToUInt16(limitations.MaxConcurrency), true);

            consumer = new EventingBasicConsumer(model);
            consumerShutdownCompleted = new TaskCompletionSource<bool>();

            consumer.Received += ConsumerOnReceived;

            consumer.Shutdown += (sender, eventArgs) =>
            {
                consumerShutdownCompleted.TrySetResult(true);
            };

            model.BasicConsume(settings.InputQueue, false, consumer);

            if (secondaryReceiveSettings.IsEnabled)
            {
                model.BasicConsume(secondaryReceiveSettings.ReceiveQueue, false, consumer);
            }
        }

        public async Task Stop()
        {
            consumer.Received -= ConsumerOnReceived;

            await Task.WhenAll(inFlightMessages.Values).ConfigureAwait(false);

            if (connection.IsOpen)
            {
                connection.Close();
            }

            await (consumerShutdownCompleted?.Task ?? TaskEx.Completed).ConfigureAwait(false);
        }

        async void ConsumerOnReceived(object sender, BasicDeliverEventArgs eventArgs)
        {
            Task task = null;

            try
            {
                circuitBreaker.Success();

                var consumer = (EventingBasicConsumer)sender;
                task = ProcessMessage(eventArgs, consumer.Model, taskScheduler.ExclusiveScheduler);
                inFlightMessages.TryAdd(task.Id, task);

                await task.ConfigureAwait(false);
            }
            finally
            {
                inFlightMessages.TryRemove(task.Id, out task);
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

                CancellationTokenSource tokenSource = null;

                if (pushMessage)
                {
                    tokenSource = new CancellationTokenSource();
                    await PushMessageToPipe(messageId, headers, tokenSource, new MemoryStream(message.Body ?? new byte[0])).ConfigureAwait(false);
                }

                var cancellationRequested = tokenSource?.IsCancellationRequested ?? false;

                if (cancellationRequested)
                {
                    var task = new Task(() => { channel.BasicReject(message.DeliveryTag, true); });
                    task.Start(scheduler);
                    await task.ConfigureAwait(false);
                }
                else
                {
                    var task = new Task(() => { channel.BasicAck(message.DeliveryTag, false); });
                    task.Start(scheduler);
                    await task.ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                var task = new Task(() => { channel.BasicReject(message.DeliveryTag, true); });
                task.Start(scheduler);
                await task.ConfigureAwait(false);
            }
        }

        async Task PushMessageToPipe(string messageId, Dictionary<string, string> headers, CancellationTokenSource tokenSource, Stream stream)
        {
            var contextBag = new ContextBag();

            string explicitCallbackAddress;

            if (headers.TryGetValue(Callbacks.HeaderKey, out explicitCallbackAddress))
            {
                contextBag.Set(new CallbackAddress(explicitCallbackAddress));
            }

            var pushContext = new PushContext(messageId, headers, stream, new TransportTransaction(), tokenSource, contextBag);

            await Task.Run(() => pipe(pushContext)).ConfigureAwait(false);
        }

        public void Dispose()
        {
            // Injected
        }
    }
}
