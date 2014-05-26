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
                    var properties = RabbitMqTransportMessageExtensions.FillRabbitMqProperties(message,
                                                                                               channel.CreateBasicProperties());

                    RoutingTopology.Publish(channel, eventType, message, properties);
                });
        }

        public RabbitMqUnitOfWork UnitOfWork { get; set; }
    }
}