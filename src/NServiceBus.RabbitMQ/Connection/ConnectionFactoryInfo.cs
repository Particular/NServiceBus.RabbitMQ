namespace NServiceBus.Transports.RabbitMQ.Connection
{
    using global::RabbitMQ.Client;

    class ConnectionFactoryInfo
    {
        public ConnectionFactoryInfo(ConnectionFactory connectionFactory, HostConfiguration hostConfiguration)
        {
            ConnectionFactory = connectionFactory;
            HostConfiguration = hostConfiguration;
        }

        public ConnectionFactory ConnectionFactory { get; private set; }
        public HostConfiguration HostConfiguration { get; private set; }
    }
}