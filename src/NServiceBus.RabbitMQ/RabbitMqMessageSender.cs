namespace NServiceBus.Transports.RabbitMQ
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using NServiceBus.Extensibility;
    using NServiceBus.Transports.RabbitMQ.Routing;

    using Headers = NServiceBus.Headers;

    class RabbitMqMessageSender : IDispatchMessages
    {
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
                    SendMessage(unicastTransportOperation, confirmsAwareChannel.Channel, context);
                }

                foreach (var multicastTransportOperation in operations.MulticastTransportOperations)
                {
                    PublishMessage(multicastTransportOperation, confirmsAwareChannel.Channel);
                }
            }

            return TaskEx.Completed;
        }

        void SendMessage(UnicastTransportOperation transportOperation, IModel channel, ContextBag context)
        {
            var message = transportOperation.Message;

            var properties = channel.CreateBasicProperties();

            RabbitMqTransportMessageExtensions.FillRabbitMqProperties(message, transportOperation.DeliveryConstraints, properties);

            var destination = DetermineDestination(transportOperation.Message.Headers, transportOperation.Destination, context);

            routingTopology.Send(channel, destination, message, properties);
        }

        void PublishMessage(MulticastTransportOperation transportOperation, IModel channel)
        {
            var message = transportOperation.Message;

            var properties = channel.CreateBasicProperties();

            RabbitMqTransportMessageExtensions.FillRabbitMqProperties(message, transportOperation.DeliveryConstraints, properties);

            routingTopology.Publish(channel, transportOperation.MessageType, message, properties);
        }

        static string DetermineDestination(Dictionary<string, string> headers, string defaultDestination, ContextBag context)
        {
            if (!IsReply(headers))
            {
                return defaultDestination;
            }

            //CallbackAddress callbackAddress;

            //if (context.TryGet(out callbackAddress))
            //{
            //    return callbackAddress.Address;
            //}

            return defaultDestination;
        }

        static bool IsReply(IReadOnlyDictionary<string, string> headers)
        {
            string intent;
            if (!headers.TryGetValue(Headers.MessageIntent, out intent))
            {
                return false;
            }

            return intent == REPLY;
        }

        static string REPLY = MessageIntentEnum.Reply.ToString();
        IChannelProvider channelProvider;
        IRoutingTopology routingTopology;
    }
}