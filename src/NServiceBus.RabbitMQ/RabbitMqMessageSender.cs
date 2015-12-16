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
        public RabbitMqMessageSender(IRoutingTopology routingTopology, IChannelProvider channelProvider, Callbacks callbacks)
        {
            this.routingTopology = routingTopology;
            this.channelProvider = channelProvider;
            this.callbacks = callbacks;
        }

        public Task Dispatch(IEnumerable<TransportOperation> outgoingMessages, ContextBag context)
        {
            using (var confirmsAwareChannel = channelProvider.GetNewPublishChannel())
            {
                foreach (var transportOperation in outgoingMessages)
                {
                    SendMessage(transportOperation, confirmsAwareChannel.Channel, context);
                }
            }

            return TaskEx.Completed;
        }

        void SendMessage(TransportOperation transportOperation, IModel channel, ContextBag context)
        {
            var dispatchOptions = transportOperation.DispatchOptions;
            var message = transportOperation.Message;

            var unicastRouting = dispatchOptions.AddressTag as UnicastAddressTag;

            var properties = channel.CreateBasicProperties();

            RabbitMqTransportMessageExtensions.FillRabbitMqProperties(message, dispatchOptions, properties);

            //todo: we can optimize this to check if there are callbacks present via the new header set by the callbacks package as well
            if (callbacks.Enabled)
            {
                properties.Headers[Callbacks.HeaderKey] = callbacks.QueueAddress;
            }

            if (unicastRouting != null)
            {
                var destination = DetermineDestination(transportOperation.Message.Headers, unicastRouting.Destination, context);

                routingTopology.Send(channel, destination, message, properties);

                return;
            }

            var multicastRouting = (MulticastAddressTag)dispatchOptions.AddressTag;

            routingTopology.Publish(channel, multicastRouting.MessageType, message, properties);
        }

        static string DetermineDestination(Dictionary<string, string> headers, string defaultDestination, ContextBag context)
        {
            if (!IsReply(headers))
            {
                return defaultDestination;
            }

            CallbackAddress callbackAddress;

            if (context.TryGet(out callbackAddress))
            {
                return callbackAddress.Address;
            }

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
        Callbacks callbacks;
        IRoutingTopology routingTopology;
    }
}