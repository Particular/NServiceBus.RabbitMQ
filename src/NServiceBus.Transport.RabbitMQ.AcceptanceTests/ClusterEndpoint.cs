namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using global::RabbitMQ.Client;
    using NServiceBus.AcceptanceTests.EndpointTemplates;

    class ClusterEndpoint : DefaultServer
    {
        public ClusterEndpoint(QueueMode queueMode)
        {
            var transportConfiguration = new ConfigureEndpointRabbitMQTransport(queueMode);
            TransportConfiguration = transportConfiguration;
        }

        public IConnection CreateConnection() =>
            (TransportConfiguration as ConfigureEndpointRabbitMQTransport).CreateConnectionFactory().CreateConnection();
    }
}