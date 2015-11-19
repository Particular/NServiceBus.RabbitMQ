namespace NServiceBus.Transports.RabbitMQ
{
    using global::RabbitMQ.Client;
    using NServiceBus.Pipeline;
    using Routing;
    using Unicast;

    class RabbitMqMessageSender : ISendMessages
    {
        IRoutingTopology routingTopology;
        IChannelProvider channelProvider;
        BehaviorContext context;

        public RabbitMqMessageSender(IRoutingTopology routingTopology, IChannelProvider channelProvider, BehaviorContext context)
        {
            this.routingTopology = routingTopology;
            this.channelProvider = channelProvider;
            this.context = context;
        }

        public void Send(TransportMessage message, SendOptions sendOptions)
        {
            IModel channel;

            if (channelProvider.TryGetPublishChannel(out channel))
            {
                SendMessage(message, sendOptions, channel);
            }
            else
            {
                using (var confirmsAwareChannel = channelProvider.GetNewPublishChannel())
                {
                    SendMessage(message, sendOptions, confirmsAwareChannel.Channel);
                }
            }
        }

        void SendMessage(TransportMessage message, SendOptions sendOptions, IModel channel)
        {
            var destination = DetermineDestination(sendOptions);
            var properties = channel.CreateBasicProperties();

            RabbitMqTransportMessageExtensions.FillRabbitMqProperties(message, sendOptions, properties);

            routingTopology.Send(channel, destination, message, properties);
        }

        Address DetermineDestination(SendOptions sendOptions)
        {
            return RequestorProvidedCallbackAddress(sendOptions) ?? SenderProvidedDestination(sendOptions);
        }

        static Address SenderProvidedDestination(SendOptions sendOptions)
        {
            return sendOptions.Destination;
        }

        Address RequestorProvidedCallbackAddress(SendOptions sendOptions)
        {
            string callbackAddress;
            if (IsReply(sendOptions) && context.TryGet(CallbackHeaderKey, out callbackAddress))
            {
                return Address.Parse(callbackAddress);
            }
            return null;
        }

        static bool IsReply(SendOptions sendOptions)
        {
            return sendOptions.GetType().FullName.EndsWith("ReplyOptions");
        }

        public const string CallbackHeaderKey = "NServiceBus.RabbitMQ.CallbackQueue";
    }
}