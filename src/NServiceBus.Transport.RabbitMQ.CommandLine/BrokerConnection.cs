namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using global::RabbitMQ.Client;
    using NServiceBus.Transport.RabbitMQ;

    class BrokerConnection
    {
        public BrokerConnection(RabbitMQ.ConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public IConnection Create()
        {
            var (connection, _) = connectionFactory.CreateAdministrationConnection();
            connection.VerifyBrokerRequirements();

            return connection;
        }

        RabbitMQ.ConnectionFactory connectionFactory;
    }
}
