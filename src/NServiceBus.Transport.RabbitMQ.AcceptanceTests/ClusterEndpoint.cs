namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using NServiceBus.AcceptanceTests.EndpointTemplates;

    class ClusterEndpoint : DefaultServer
    {
        public ClusterEndpoint(QueueMode queueMode, Timeouts timeoutsConfiguration)
        {
            var transportConfiguration = new ConfigureEndpointRabbitMQTransport(queueMode, timeoutsConfiguration == Timeouts.UnsafeEnabled);
            TransportConfiguration = transportConfiguration;
        }
    }
}