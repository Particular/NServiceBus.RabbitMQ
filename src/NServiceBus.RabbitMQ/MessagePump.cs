namespace NServiceBus.Transports.RabbitMQ
{
    using Extensibility;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using NServiceBus.Transports.RabbitMQ.Routing;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using NServiceBus.Logging;

    class MessagePump : IPushMessages
    {
        readonly IManageRabbitMqConnections connectionManager;
        readonly ReceiveOptions receiveOptions;
        readonly IRoutingTopology routingTopology;
        readonly IChannelProvider channelProvider;

        Func<PushContext, Task> pipe;
        //CriticalError criticalError;
        PushSettings settings;
        SecondaryReceiveSettings secondaryReceiveSettings;

        EventingBasicConsumer consumer;
        TaskCompletionSource<bool> consumerShutdownCompleted;
        private bool noAck;

        public MessagePump(IManageRabbitMqConnections connectionManager, IRoutingTopology routingTopology, IChannelProvider channelProvider, ReceiveOptions receiveOptions)
        {
            this.connectionManager = connectionManager;
            this.receiveOptions = receiveOptions;
            this.routingTopology = routingTopology;
            this.channelProvider = channelProvider;
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
                Purge(settings.InputQueue);
            }

            return TaskEx.Completed;
        }

        public void Start(PushRuntimeSettings limitations)
        {
            var connection = connectionManager.GetConsumeConnection();
            var model = connection.CreateModel();
            model.BasicQos(0, GetPrefetchGount(limitations.MaxConcurrency), false);

            consumer = new EventingBasicConsumer(model);
            consumerShutdownCompleted = new TaskCompletionSource<bool>();

            consumer.Received += (sender, eventArgs) =>
            {
                var originalConsumer = (EventingBasicConsumer)sender;
                ProcessMessage(eventArgs, originalConsumer.Model).GetAwaiter().GetResult();
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

        ushort GetPrefetchGount(int max)
        {
            ushort actualPrefetchCount;
            if (receiveOptions.DefaultPrefetchCount > 0)
            {
                actualPrefetchCount = receiveOptions.DefaultPrefetchCount;
            }
            else
            {
                actualPrefetchCount = Convert.ToUInt16(max);

                Logger.InfoFormat("No prefetch count configured, defaulting to {0} (the configured concurrency level)", actualPrefetchCount);
            }
            return actualPrefetchCount;
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
                    ForwardPoisonMessageToErrorQueue(message, ex);
                }

                if (pushMessage)
                {
                    await PushMessageToPipe(messageId, headers, new MemoryStream(message.Body ?? new byte[0]));
                }

                if (!noAck)
                {
                    channel.BasicAck(message.DeliveryTag, false);
                }
            }
            catch (Exception)
            {
                if (!noAck)
                {
                    channel.BasicReject(message.DeliveryTag, true);
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

            await pipe(pushContext).ConfigureAwait(false);
        }

        void ForwardPoisonMessageToErrorQueue(BasicDeliverEventArgs message, Exception ex)
        {
            var error = $"Poison message detected with deliveryTag '{message.DeliveryTag}'. Message will be moved to '{settings.ErrorQueue}'.";
            Logger.Error(error, ex);

            try
            {
                using (var errorChannel = channelProvider.GetNewPublishChannel())
                {
                    routingTopology.RawSendInCaseOfFailure(errorChannel.Channel, settings.ErrorQueue, message.Body, message.BasicProperties);
                }
            }
            catch (Exception ex2)
            {
                Logger.Error($"Poison message failed to be moved to '{settings.ErrorQueue}'.", ex2);
                throw;
            }
        }

        void Purge(string queue)
        {
            using (var connection = connectionManager.GetAdministrationConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueuePurge(queue);
            }
        }

        public Task Stop()
        {
            consumer?.Model?.Close();

            return consumerShutdownCompleted?.Task ?? TaskEx.Completed;
        }

        static ILog Logger = LogManager.GetLogger(typeof(MessagePump));
    }
}
