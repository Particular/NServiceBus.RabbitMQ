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

            var connectionFactory = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                UseBackgroundThreadsForIO = true
            };

            if (connectionStringBuilder.TryGetValue("username", out var value))
            {
                connectionFactory.UserName = value.ToString();
            }

            if (connectionStringBuilder.TryGetValue("password", out value))
            {
                connectionFactory.Password = value.ToString();
            }

            if (connectionStringBuilder.TryGetValue("virtualhost", out value))
            {
                connectionFactory.VirtualHost = value.ToString();
            }

            if (connectionStringBuilder.TryGetValue("host", out value))
            {
                connectionFactory.HostName = value.ToString();
            }
            else
            {
                throw new Exception("The connection string doesn't contain a value for 'host'.");
            }

            return connectionFactory;
        });

        public static string ConnectionString => connectionString.Value;

        public static ConnectionFactory ConnectionFactory => connectionFactory.Value;
    }
}