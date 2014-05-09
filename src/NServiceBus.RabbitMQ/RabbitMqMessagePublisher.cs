namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Routing;

    public class RabbitMqMessagePublisher : IPublishMessages
    {
        public IRoutingTopology RoutingTopology { get; set; }

        public void Publish(TransportMessage message, IEnumerable<Type> eventTypes)
        {
            var eventType = eventTypes.First();//we route on the first event for now

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