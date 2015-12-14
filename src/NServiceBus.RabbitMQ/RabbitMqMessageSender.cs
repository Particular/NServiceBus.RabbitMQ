namespace NServiceBus.Transports.RabbitMQ
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using NServiceBus.Extensibility;
    using NServiceBus.Routing;
    using Routing;

    class RabbitMqMessageSender : IDispatchMessages
    {
        IRoutingTopology routingTopology;
        IChannelProvider channelProvider;

        public RabbitMqMessageSender(IRoutingTopology routingTopology, IChannelProvider channelProvider)
        {
            this.routingTopology = routingTopology;
            this.channelProvider = channelProvider;
        }


        public const string CallbackHeaderKey = "NServiceBus.RabbitMQ.CallbackQueue";

        public Task Dispatch(IEnumerable<TransportOperation> outgoingMessages, ContextBag context)
        {
           IModel channel;

            if (channelProvider.TryGetPublishChannel(context, out channel))
            {
                SendMessages(outgoingMessages, channel);
            }
            else
            {
                using (var confirmsAwareChannel = channelProvider.GetNewPublishChannel())
                {
                    SendMessages(outgoingMessages, confirmsAwareChannel.Channel);
                }
            }

            return Task.FromResult(0);
        }

        private void SendMessages(IEnumerable<TransportOperation> outgoingMessages, IModel channel)
        {
            foreach (var transportOperation in outgoingMessages)
            {
                SendMessage(transportOperation, channel);
            }
        }

        private void SendMessage(TransportOperation transportOperation, IModel channel)
        {
            var dispatchOptions = transportOperation.DispatchOptions;
            var message = transportOperation.Message;

            var unicastRouting = dispatchOptions.AddressTag as UnicastAddressTag;

            var properties = channel.CreateBasicProperties();

            RabbitMqTransportMessageExtensions.FillRabbitMqProperties(message, dispatchOptions, properties);

            if (unicastRouting != null)
            {
                var destination = DetermineDestination(unicastRouting);

                routingTopology.Send(channel, destination, message, properties);

                return;
            }

            var multicastRouting = (MulticastAddressTag)dispatchOptions.AddressTag;

            routingTopology.Publish(channel, multicastRouting.MessageType, message, properties);
        }

        string DetermineDestination(UnicastAddressTag sendOptions)
        {
            return RequestorProvidedCallbackAddress(sendOptions) ?? SenderProvidedDestination(sendOptions);
        }

        static string SenderProvidedDestination(UnicastAddressTag sendOptions)
        {
            return sendOptions.Destination;
        }

        string RequestorProvidedCallbackAddress(UnicastAddressTag sendOptions)
        {
            //TODO: Still need to deal with callback address, especially for backwards compatibility

            //string callbackAddress;
            //if (IsReply(sendOptions) && context.TryGet(CallbackHeaderKey, out callbackAddress))
            //{
            //    return callbackAddress;
            //}
            return null;
        }

        static bool IsReply(SendOptions sendOptions)
        {
            return sendOptions.GetType().FullName.EndsWith("ReplyOptions");
        }
    }
}