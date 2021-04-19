namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using NServiceBus.AcceptanceTests.EndpointTemplates;

    class ClusterEndpoint : DefaultServer
    {
        public ClusterEndpoint(QueueMode queueMode)
        {
            var transportConfiguration = new ConfigureEndpointRabbitMQTransport(queueMode);
            TransportConfiguration = transportConfiguration;
        }
    }
}