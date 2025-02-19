namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using global::RabbitMQ.Client;

    class BrokerConnection(BrokerVerifier brokerVerifier, RabbitMQ.ConnectionFactory connectionFactory)
    {
        public async Task<IConnection> Create(CancellationToken cancellationToken = default)
        {
            await brokerVerifier.Initialize(cancellationToken);
            await brokerVerifier.VerifyRequirements(cancellationToken);

            var connection = await connectionFactory.CreateAdministrationConnection(cancellationToken);

            return connection;
        }
    }
}
