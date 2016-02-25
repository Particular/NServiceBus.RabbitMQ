namespace NServiceBus.Transports.RabbitMQ
{
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using NServiceBus.Extensibility;
    using NServiceBus.Transports.RabbitMQ.Routing;

    class RabbitMqMessageSender : IDispatchMessages
    {
        readonly IChannelProvider channelProvider;
        readonly IRoutingTopology routingTopology;

        public RabbitMqMessageSender(IRoutingTopology routingTopology, IChannelProvider channelProvider)
        {
            this.routingTopology = routingTopology;
            this.channelProvider = channelProvider;
        }

        public Task Dispatch(TransportOperations operations, ContextBag context)
        {
            using (var confirmsAwareChannel = channelProvider.GetNewPublishChannel())
            {
                foreach (var unicastTransportOperation in operations.UnicastTransportOperations)
                {
                    SendMessage(unicastTransportOperation, confirmsAwareChannel.Channel);
                }

                foreach (var multicastTransportOperation in operations.MulticastTransportOperations)
                {
                    PublishMessage(multicastTransportOperation, confirmsAwareChannel.Channel);
                }
            }

            return TaskEx.Completed;
        }

        void SendMessage(UnicastTransportOperation transportOperation, IModel channel)
        {
            var message = transportOperation.Message;

            var properties = channel.CreateBasicProperties();
            RabbitMqTransportMessageExtensions.FillRabbitMqProperties(message, transportOperation.DeliveryConstraints, properties);

            routingTopology.Send(channel, transportOperation.Destination, message, properties);
        }

        void PublishMessage(MulticastTransportOperation transportOperation, IModel channel)
        {
            var message = transportOperation.Message;

            var properties = channel.CreateBasicProperties();
            RabbitMqTransportMessageExtensions.FillRabbitMqProperties(message, transportOperation.DeliveryConstraints, properties);

            routingTopology.Publish(channel, transportOperation.MessageType, message, properties);
        }
    }
}