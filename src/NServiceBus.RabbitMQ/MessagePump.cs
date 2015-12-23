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
        readonly IRoutingTopology routingTopology;
        readonly IChannelProvider channelProvider;

        Func<PushContext, Task> pipe;
        CriticalError criticalError;
        PushSettings settings;

        EventingBasicConsumer consumer;

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
            this.criticalError = criticalError;
            this.settings = settings;

            return TaskEx.Completed;
        }

        public void Start(PushRuntimeSettings limitations)
        {
            var connection = connectionManager.GetConsumeConnection();
            var model = connection.CreateModel();

            consumer = new EventingBasicConsumer(model);

            consumer.Received += (sender, eventArgs) =>
            {
                var blah = (EventingBasicConsumer)sender;
                ProcessMessage(eventArgs, blah.Model).GetAwaiter().GetResult();
            };

            model.BasicConsume(settings.InputQueue, true, consumer);
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
            var model = consumer?.Model;

            model?.BasicCancel(consumer.ConsumerTag);

            return TaskEx.Completed;
        }
    }
}
