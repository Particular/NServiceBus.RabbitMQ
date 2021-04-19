namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System;
    using System.Data.Common;
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
            var connectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = ConnectionString };

            var factory = new ConnectionFactory { AutomaticRecoveryEnabled = true, UseBackgroundThreadsForIO = true };

            if (connectionStringBuilder.TryGetValue("username", out var value))
            {
                factory.UserName = value.ToString();
            }

            if (connectionStringBuilder.TryGetValue("password", out value))
            {
                factory.Password = value.ToString();
            }

            if (connectionStringBuilder.TryGetValue("virtualhost", out value))
            {
                factory.VirtualHost = value.ToString();
            }

            if (connectionStringBuilder.TryGetValue("host", out value))
            {
                factory.HostName = value.ToString();
            }
            else
            {
                throw new Exception("The connection string doesn't contain a value for 'host'.");
            }

            return factory;
        });

        public static string ConnectionString => connectionString.Value;

        public static ConnectionFactory ConnectionFactory => connectionFactory.Value;
    }
}