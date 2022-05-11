namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using NServiceBus.AcceptanceTests.EndpointTemplates;

    class ClusterEndpoint : DefaultServer
    {
        public ClusterEndpoint(QueueType queueType)
        {
            var transportConfiguration = new ConfigureEndpointRabbitMQTransport(queueType);
            TransportConfiguration = transportConfiguration;
        }
    }
}