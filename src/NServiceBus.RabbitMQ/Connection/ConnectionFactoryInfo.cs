namespace NServiceBus.Transports.RabbitMQ.Connection
{
    using global::RabbitMQ.Client;

    class ConnectionFactoryInfo
    {
        public ConnectionFactoryInfo(ConnectionFactory connectionFactory, IHostConfiguration hostConfiguration)
        {
            ConnectionFactory = connectionFactory;
            HostConfiguration = hostConfiguration;
        }

        public ConnectionFactory ConnectionFactory { get; private set; }
        public IHostConfiguration HostConfiguration { get; private set; }
    }
}