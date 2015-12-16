namespace NServiceBus.Transports.RabbitMQ
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using NServiceBus.Extensibility;
    using NServiceBus.Routing;
    using NServiceBus.Transports.RabbitMQ.Routing;

    class RabbitMqMessageSender : IDispatchMessages
    {
        public const string CallbackHeaderKey = "NServiceBus.RabbitMQ.CallbackQueue";
        private static readonly string REPLY = MessageIntentEnum.Reply.ToString();
        IChannelProvider channelProvider;
        IRoutingTopology routingTopology;

        public RabbitMqMessageSender(IRoutingTopology routingTopology, IChannelProvider channelProvider)
        {
            this.routingTopology = routingTopology;
            this.channelProvider = channelProvider;
        }

        public Task Dispatch(IEnumerable<TransportOperation> outgoingMessages, ContextBag context)
        {
            using (var confirmsAwareChannel = channelProvider.GetNewPublishChannel())
            {
                foreach (var transportOperation in outgoingMessages)
                {
                    SendMessage(transportOperation, confirmsAwareChannel.Channel);
                }
            }

            return TaskEx.Completed;
        }

        void SendMessage(TransportOperation transportOperation, IModel channel)
        {
            var dispatchOptions = transportOperation.DispatchOptions;
            var message = transportOperation.Message;

            var unicastRouting = dispatchOptions.AddressTag as UnicastAddressTag;

            var properties = channel.CreateBasicProperties();

            RabbitMqTransportMessageExtensions.FillRabbitMqProperties(message, dispatchOptions, properties);

            if (unicastRouting != null)
            {
                var destination = DetermineDestination(transportOperation, unicastRouting);

                routingTopology.Send(channel, destination, message, properties);

                return;
            }

            var multicastRouting = (MulticastAddressTag) dispatchOptions.AddressTag;

            routingTopology.Publish(channel, multicastRouting.MessageType, message, properties);
        }

        static string DetermineDestination(TransportOperation transportOperation, UnicastAddressTag sendOptions)
        {
            return RequestorProvidedCallbackAddress(transportOperation.Message.Headers) ?? SenderProvidedDestination(sendOptions);
        }

        static string SenderProvidedDestination(UnicastAddressTag sendOptions)
        {
            return sendOptions.Destination;
        }

        static string RequestorProvidedCallbackAddress(IReadOnlyDictionary<string, string> headers)
        {
            string callbackAddress;
            if (IsReply(headers) && headers.TryGetValue(CallbackHeaderKey, out callbackAddress))
            {
                return callbackAddress;
            }

            return null;
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
    }
}