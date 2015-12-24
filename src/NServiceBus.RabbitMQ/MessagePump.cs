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

    class MessagePump : IPushMessages
    {
        readonly IManageRabbitMqConnections connectionManager;
        readonly ReceiveOptions receiveOptions;
        //readonly IRoutingTopology routingTopology;
        //readonly IChannelProvider channelProvider;

        Func<PushContext, Task> pipe;
        //CriticalError criticalError;
        PushSettings settings;
        SecondaryReceiveSettings secondaryReceiveSettings;

        EventingBasicConsumer consumer;
        TaskCompletionSource<bool> consumerShutdownCompleted;

        public MessagePump(IManageRabbitMqConnections connectionManager, IRoutingTopology routingTopology, IChannelProvider channelProvider, ReceiveOptions receiveOptions)
        {
            this.connectionManager = connectionManager;
            this.receiveOptions = receiveOptions;
            //this.routingTopology = routingTopology;
            //this.channelProvider = channelProvider;
        }

        public Task Init(Func<PushContext, Task> pipe, CriticalError criticalError, PushSettings settings)
        {
            this.pipe = pipe;
            //this.criticalError = criticalError;
            this.settings = settings;

            secondaryReceiveSettings = receiveOptions.GetSettings(settings.InputQueue);

            return TaskEx.Completed;
        }

        public void Start(PushRuntimeSettings limitations)
        {
            var connection = connectionManager.GetConsumeConnection();
            var model = connection.CreateModel();

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

            model.BasicConsume(settings.InputQueue, true, consumer);

            if (secondaryReceiveSettings.IsEnabled)
            {
                model.BasicConsume(secondaryReceiveSettings.ReceiveQueue, true, consumer);
            }
        }

        async Task ProcessMessage(BasicDeliverEventArgs message, IModel channel)
        {
            var messageId = receiveOptions.Converter.RetrieveMessageId(message);
            var headers = receiveOptions.Converter.RetrieveHeaders(message);

            var contextBag = new ContextBag();

            var pushContext = new PushContext(messageId, headers, new MemoryStream(message.Body ?? new byte[0]), new TransportTransaction(), contextBag);

            await pipe(pushContext).ConfigureAwait(false);

        }

        public Task Stop()
        {
            consumer?.Model?.Close();

            return consumerShutdownCompleted?.Task ?? TaskEx.Completed;
        }
    }
}
