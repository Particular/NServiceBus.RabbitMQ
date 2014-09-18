namespace NServiceBus.Transports.RabbitMQ
{
    using global::RabbitMQ.Client;
    using Routing;
    using Unicast;

    class RabbitMqMessagePublisher : IPublishMessages
    {
        public IRoutingTopology RoutingTopology { get; set; }

        public IChannelProvider ChannelProvider { get; set; }

        public void Publish(TransportMessage message, PublishOptions publishOptions)
        {
        
            IModel channel;

            if (ChannelProvider.TryGetPublishChannel(out channel))
            {
                PublishMessage(message,publishOptions,channel);
            }
            else
            {
                using (var confirmsAwareChannel = ChannelProvider.GetNewPublishChannel())
                {
                    PublishMessage(message, publishOptions, confirmsAwareChannel.Channel);    
                }
                
            }
            
        }

        void PublishMessage(TransportMessage message, PublishOptions publishOptions, IModel channel)
        {
            var eventType = publishOptions.EventType;

            var properties = channel.CreateBasicProperties();

            RabbitMqTransportMessageExtensions.FillRabbitMqProperties(message, publishOptions, properties);

            RoutingTopology.Publish(channel, eventType, message, properties);
        }
    }
}