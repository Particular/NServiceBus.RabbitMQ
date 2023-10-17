using System;
using System.Security.Authentication;
using NServiceBus.Transport.RabbitMQ;
using RabbitMQ.Client;

public class ConnectionHelper
{
    static Lazy<string> connectionString = new Lazy<string>(() =>
    {
        var connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString") ?? "host=localhost";

        return connectionString;
    });

    static Lazy<ConnectionFactory> connectionFactory = new Lazy<ConnectionFactory>(() =>
    {
        var connectionStringParser = ConnectionConfiguration.Create(ConnectionString);

        var factory = new ConnectionFactory
        {
            AutomaticRecoveryEnabled = true,
            HostName = connectionStringParser.Host,
            Port = connectionStringParser.Port,
            VirtualHost = connectionStringParser.VirtualHost,
            UserName = connectionStringParser.UserName ?? "guest",
            Password = connectionStringParser.Password ?? "guest"
        };

        factory.Ssl.ServerName = factory.HostName;
        factory.Ssl.Certs = null;
        factory.Ssl.Version = SslProtocols.Tls12;
        factory.Ssl.Enabled = connectionStringParser.UseTls;

        return factory;
    });

    public static string ConnectionString => connectionString.Value;

    public static ConnectionFactory ConnectionFactory => connectionFactory.Value;
}
