namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using NServiceBus.AcceptanceTests.EndpointTemplates;

    class ClusterEndpoint : DefaultServer
    {
        public ClusterEndpoint(QueueMode queueMode, DelayedDeliverySupport delayedDeliveryConfiguration)
        {
            var transportConfiguration = new ConfigureEndpointRabbitMQTransport(queueMode, delayedDeliveryConfiguration == DelayedDeliverySupport.UnsafeEnabled);
            TransportConfiguration = transportConfiguration;
        }
    }
}