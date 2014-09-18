namespace NServiceBus.Transports.RabbitMQ
{
    using global::RabbitMQ.Client;
    using Routing;
    using Unicast;

    class RabbitMqMessageSender : ISendMessages
    {
        public IRoutingTopology RoutingTopology { get; set; }

        public IChannelProvider ChannelProvider { get; set; }

        public string CallbackQueue { get; set; }

        public void Send(TransportMessage message, SendOptions sendOptions)
        {
            IModel channel;

            if (ChannelProvider.TryGetPublishChannel(out channel))
            {
                SendMessage(message, sendOptions, channel);
            }
            else
            {
                using (var confirmsAwareChannel = ChannelProvider.GetNewPublishChannel())
                {
                    SendMessage(message, sendOptions, confirmsAwareChannel.Channel);
                }
            }
        }

        void SendMessage(TransportMessage message, SendOptions sendOptions, IModel channel)
        {

            var destination = sendOptions.Destination;

            string callbackAddress;

            if (message.MessageIntent == MessageIntentEnum.Reply &&
                message.Headers.TryGetValue(CallbackHeaderKey, out callbackAddress))
            {
                destination = Address.Parse(callbackAddress);
            }

            //set our callback address
            message.Headers[CallbackHeaderKey] = CallbackQueue;

            var properties = channel.CreateBasicProperties();

            RabbitMqTransportMessageExtensions.FillRabbitMqProperties(message, sendOptions, properties);

            RoutingTopology.Send(channel, destination, message, properties);
        }

        public const string CallbackHeaderKey = "NServiceBus.RabbitMQ.CallbackQueue";
    }
}