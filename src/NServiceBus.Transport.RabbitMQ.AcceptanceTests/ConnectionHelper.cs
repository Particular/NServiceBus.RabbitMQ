namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System;
    using global::RabbitMQ.Client;

    public class ConnectionHelper
    {
        static Lazy<string> connectionString = new Lazy<string>(() =>
        {
            return Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString") ?? "host=localhost";
        });

        static Lazy<ConnectionFactory> connectionFactory = new Lazy<ConnectionFactory>(() =>
        {
            var connectionConfiguration = ConnectionConfiguration.Create(ConnectionString);

            var connectionFactory = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                UseBackgroundThreadsForIO = true,
                HostName = connectionConfiguration.Host,
                Port = connectionConfiguration.Port,
                VirtualHost = connectionConfiguration.VirtualHost,
                UserName = connectionConfiguration.UserName ?? "guest",
                Password = connectionConfiguration.Password ?? "guest"
            };

            return connectionFactory;
        });

        public static string ConnectionString => connectionString.Value;

        public static ConnectionFactory ConnectionFactory => connectionFactory.Value;
    }
}