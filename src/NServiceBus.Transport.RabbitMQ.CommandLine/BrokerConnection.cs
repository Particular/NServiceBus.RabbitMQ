namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using global::RabbitMQ.Client;

    class BrokerConnection
    {
        public BrokerConnection(RabbitMQ.ConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public async Task<IConnection> Create(CancellationToken cancellationToken = default)
        {
            var connection = await connectionFactory.CreateAdministrationConnection(cancellationToken);

            //TODO Decide how to handle broker verification in commandline
            //await connection.VerifyBrokerRequirements(cancellationToken: cancellationToken);

            return connection;
        }

        RabbitMQ.ConnectionFactory connectionFactory;
    }
}
