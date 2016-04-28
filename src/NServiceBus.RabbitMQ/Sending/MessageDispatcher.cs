﻿namespace NServiceBus.Transport.RabbitMQ
{
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using NServiceBus.Extensibility;
    using NServiceBus.Transports;

    class MessageDispatcher : IDispatchMessages
    {
        readonly IChannelProvider channelProvider;
        readonly IRoutingTopology routingTopology;

        public MessageDispatcher(IRoutingTopology routingTopology, IChannelProvider channelProvider)
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

            return TaskEx.CompletedTask;
        }

        void SendMessage(UnicastTransportOperation transportOperation, IModel channel)
        {
            var message = transportOperation.Message;

            var properties = channel.CreateBasicProperties();
            properties.Fill(message, transportOperation.DeliveryConstraints);

            routingTopology.Send(channel, transportOperation.Destination, message, properties);
        }

        void PublishMessage(MulticastTransportOperation transportOperation, IModel channel)
        {
            var message = transportOperation.Message;

            var properties = channel.CreateBasicProperties();
            properties.Fill(message, transportOperation.DeliveryConstraints);

            routingTopology.Publish(channel, transportOperation.MessageType, message, properties);
        }
    }
}