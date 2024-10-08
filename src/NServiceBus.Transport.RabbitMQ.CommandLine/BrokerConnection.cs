﻿namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using global::RabbitMQ.Client;
    using NServiceBus.Transport.RabbitMQ;

    class BrokerConnection
    {
        public BrokerConnection(RabbitMQ.ConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public async Task<IConnection> Create(CancellationToken cancellationToken = default)
        {
            var connection = await connectionFactory.CreateAdministrationConnection(cancellationToken);
            await connection.VerifyBrokerRequirements(cancellationToken: cancellationToken);

            return connection;
        }

        RabbitMQ.ConnectionFactory connectionFactory;
    }
}
