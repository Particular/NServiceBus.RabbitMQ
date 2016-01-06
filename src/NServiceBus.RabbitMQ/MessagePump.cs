namespace NServiceBus.Transports.RabbitMQ
{
    using Extensibility;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using System;
    using System.Collections.Generic;
    using System.IO;
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

        Func<PushContext, Task> pipe;
        //CriticalError criticalError;
        PushSettings settings;
        SecondaryReceiveSettings secondaryReceiveSettings;
        bool noAck;

        PersistentConnection connection;
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
            //this.criticalError = criticalError;
            this.settings = settings;

            secondaryReceiveSettings = receiveOptions.GetSettings(settings.InputQueue);
            noAck = settings.RequiredTransactionMode == TransportTransactionMode.None;

            if (settings.PurgeOnStartup)
            {
                queuePurger.Purge(settings.InputQueue);
            }

            return TaskEx.Completed;
        }

        public void Start(PushRuntimeSettings limitations)
        {
            var taskScheduler = new ConcurrentExclusiveSchedulerPair(TaskScheduler.Default, limitations.MaxConcurrency);
            var factory = new RabbitMqConnectionFactory(connectionConfiguration, taskScheduler.ConcurrentScheduler);
            connection = new PersistentConnection(factory, connectionConfiguration.RetryDelay, "Consume");

            var model = connection.CreateModel();
            model.BasicQos(0, Convert.ToUInt16(limitations.MaxConcurrency), false);

            var consumer = new EventingBasicConsumer(model);
            consumerShutdownCompleted = new TaskCompletionSource<bool>();

            consumer.Received += async (sender, eventArgs) =>
            {
                var originalConsumer = (EventingBasicConsumer)sender;
                await ProcessMessage(eventArgs, originalConsumer.Model, taskScheduler.ExclusiveScheduler).ConfigureAwait(false);
            };

            consumer.Shutdown += (sender, eventArgs) =>
            {
                consumerShutdownCompleted.TrySetResult(true);
            };

            model.BasicConsume(settings.InputQueue, noAck, consumer);

            if (secondaryReceiveSettings.IsEnabled)
            {
                model.BasicConsume(secondaryReceiveSettings.ReceiveQueue, noAck, consumer);
            }
        }

        //In this MessagePump, this shouldn't be a configurable option
        //ushort GetPrefetchGount(int max)
        //{
        //    ushort actualPrefetchCount;
        //    if (receiveOptions.DefaultPrefetchCount > 0)
        //    {
        //        actualPrefetchCount = receiveOptions.DefaultPrefetchCount;
        //    }
        //    else
        //    {
        //        actualPrefetchCount = Convert.ToUInt16(max);

        //        Logger.InfoFormat("No prefetch count configured, defaulting to {0} (the configured concurrency level)", actualPrefetchCount);
        //    }
        //    return actualPrefetchCount;
        //}

        async Task ProcessMessage(BasicDeliverEventArgs message, IModel channel, TaskScheduler taskScheduler)
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
                    var task = new Task(() => { if (channel.IsOpen) channel.BasicAck(message.DeliveryTag, false); });
                    task.Start(taskScheduler);
                    await task.ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                if (!noAck)
                {
                    var task = new Task(() => { if (channel.IsOpen) channel.BasicReject(message.DeliveryTag, true); });
                    task.Start(taskScheduler);
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

        public Task Stop()
        {
            var connectionIsOpen = connection?.IsOpen ?? false;

            if (connectionIsOpen)
            {
                connection.Close();
            }

            return consumerShutdownCompleted?.Task ?? TaskEx.Completed;
        }

        public void Dispose()
        {
            // Injected
        }
    }
}
