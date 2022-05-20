namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System;
    using System.Security.Authentication;
    using global::RabbitMQ.Client;

    public class ConnectionHelper
    {
        static Lazy<string> connectionString = new Lazy<string>(() =>
        {
            var connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString") ?? "host=localhost";

            return connectionString;
        });

        static Lazy<ConnectionFactory> connectionFactory = new Lazy<ConnectionFactory>(() =>
        {
            var connectionConfiguration = ConnectionConfiguration.Create(ConnectionString);

            var factory = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                UseBackgroundThreadsForIO = true,
                HostName = connectionConfiguration.Host,
                Port = connectionConfiguration.Port,
                VirtualHost = connectionConfiguration.VirtualHost,
                UserName = connectionConfiguration.UserName ?? "guest",
                Password = connectionConfiguration.Password ?? "guest"
            };

            factory.Ssl.ServerName = factory.HostName;
            factory.Ssl.Certs = null;
            factory.Ssl.Version = SslProtocols.Tls12;
            factory.Ssl.Enabled = connectionConfiguration.UseTls;

            return factory;
        });

        public static string ConnectionString => connectionString.Value;

        public static ConnectionFactory ConnectionFactory => connectionFactory.Value;
    }
}