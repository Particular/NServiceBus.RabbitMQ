namespace NServiceBus.Transport.RabbitMQ
{
    using System;

    class AmqpConnectionString
    {
        public static Action<RabbitMQTransport> Parse(string connectionString)
        {
            return transport =>
            {
                var connectionConfiguration = ConnectionConfiguration.Create(connectionString);
                transport.ConnectionConfiguration = connectionConfiguration;
            };
        }
    }
}