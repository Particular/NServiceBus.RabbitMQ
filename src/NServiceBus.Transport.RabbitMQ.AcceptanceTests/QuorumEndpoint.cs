namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using NServiceBus.AcceptanceTests.EndpointTemplates;

    class QuorumEndpoint : DefaultServer
    {
        public QuorumEndpoint()
        {
            var transportConfiguration = new ConfigureEndpointRabbitMQTransport(QueueType.Quorum);
            TransportConfiguration = transportConfiguration;
        }
    }
}