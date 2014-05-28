namespace NServiceBus.Transports.RabbitMQ
{
    using Routing;
    using Unicast;

    class RabbitMqMessageSender : ISendMessages
    {
        public IRoutingTopology RoutingTopology { get; set; }

        public void Send(TransportMessage message, SendOptions sendOptions)
        {
            UnitOfWork.Add(channel =>
            {
                var properties = channel.CreateBasicProperties();
                RabbitMqTransportMessageExtensions.FillRabbitMqProperties(message, sendOptions, properties);
                RoutingTopology.Send(channel, sendOptions.Destination, message, properties);
            });
        }

        public RabbitMqUnitOfWork UnitOfWork { get; set; }
    }
}