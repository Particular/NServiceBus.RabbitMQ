namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System;
    using System.Security.Authentication;
    using global::RabbitMQ.Client;

    public class ConnectionHelper
    {
        static Lazy<string> connectionString = new Lazy<string>(() =>
        {
            var connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString");

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new Exception("The 'RabbitMQTransport_ConnectionString' environment variable is not set.");
            }

            return connectionString;
        });

        static Lazy<ConnectionFactory> connectionFactory = new Lazy<ConnectionFactory>(() =>
        {
            var connectionStringParser = new RabbitMqConnectionStringParser(ConnectionString);

            var factory = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                UseBackgroundThreadsForIO = true
            };

            factory.UserName = connectionStringParser.UserName ?? "guest";
            factory.Password = connectionStringParser.Password ?? "guest";

            if (!string.IsNullOrEmpty(connectionStringParser.VirtualHost))
            {
                factory.VirtualHost = connectionStringParser.VirtualHost;
            }

            if (connectionStringParser.Port.HasValue)
            {
                factory.Port = connectionStringParser.Port.Value;
            }

            if (!string.IsNullOrEmpty(connectionStringParser.HostName))
            {
                factory.HostName = connectionStringParser.HostName;
            }
            else
            {
                throw new Exception("The connection string doesn't contain a value for 'host'.");
            }

            factory.Ssl.ServerName = factory.HostName;
            factory.Ssl.Certs = null;
            factory.Ssl.Version = SslProtocols.Tls12;
            factory.Ssl.Enabled = connectionStringParser.IsTls;

            return factory;
        });

        public static string ConnectionString => connectionString.Value;

        public static ConnectionFactory ConnectionFactory => connectionFactory.Value;
    }
}