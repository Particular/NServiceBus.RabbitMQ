namespace NServiceBus.Transports.RabbitMQ
{
    using Routing;
    using Unicast;

    class RabbitMqMessagePublisher : IPublishMessages
    {
        public IRoutingTopology RoutingTopology { get; set; }

        public void Publish(TransportMessage message, PublishOptions publishOptions)
        {
            var eventType = publishOptions.EventType;

            UnitOfWork.Add(channel =>
            {
                var properties = channel.CreateBasicProperties();

                RabbitMqTransportMessageExtensions.FillRabbitMqProperties(message, publishOptions, properties);

                RoutingTopology.Publish(channel, eventType, message, properties);
            });
        }

        public RabbitMqUnitOfWork UnitOfWork { get; set; }
    }
}