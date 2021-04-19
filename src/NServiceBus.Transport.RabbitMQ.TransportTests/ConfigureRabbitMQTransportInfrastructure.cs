using System;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Transport;
using NServiceBus.TransportTests;
using RabbitMQ.Client;

// Workaround to prevent errors because scanning expects this type to exist
class ConfigureRabbitMQClusterTransportInfrastructure : ConfigureRabbitMQTransportInfrastructure
{
}

class ConfigureRabbitMQTransportInfrastructure : IConfigureTransportInfrastructure
{
    public TransportDefinition CreateTransportDefinition()
    {
        var connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new Exception("The 'RabbitMQTransport_ConnectionString' environment variable is not set.");
        }

        connectionStringBuilder = new RabbitMqConnectionStringParser(connectionString);

        var transport = new RabbitMQTransport(Topology.Conventional, connectionString);

        return transport;
    }

    public Task<TransportInfrastructure> Configure(TransportDefinition transportDefinition, HostSettings hostSettings, string inputQueueName,
        string errorQueueName, CancellationToken cancellationToken = default)
    {
        queuesToCleanUp = new[] { inputQueueName, errorQueueName };

        var mainReceiverSettings = new ReceiveSettings(
            "mainReceiver",
            inputQueueName,
            true,
            true, errorQueueName);

        return transportDefinition.Initialize(hostSettings, new[] { mainReceiverSettings }, new[] { errorQueueName }, cancellationToken);
    }

    public Task Cleanup(CancellationToken cancellationToken = default)
    {
        PurgeQueues(connectionStringBuilder, queuesToCleanUp);
        return Task.FromResult(0);
    }

    static void PurgeQueues(RabbitMqConnectionStringParser connectionStringParser, string[] queues)
    {
        if (connectionStringParser == null || queues == null)
        {
            return;
        }

        var connectionFactory = new ConnectionFactory
        {
            AutomaticRecoveryEnabled = true,
            UseBackgroundThreadsForIO = true
        };

        connectionFactory.UserName = connectionStringParser.UserName;
        connectionFactory.Password = connectionStringParser.Password;
        connectionFactory.HostName = connectionStringParser.HostName;
        connectionFactory.VirtualHost = "/";

        if (connectionStringParser.Port > 0)
        {
            connectionFactory.Port = connectionStringParser.Port;
        }

        if (!string.IsNullOrWhiteSpace(connectionStringParser.VirtualHost))
        {
            connectionFactory.VirtualHost = connectionStringParser.VirtualHost;
        }

        if (string.IsNullOrWhiteSpace(connectionFactory.HostName))
        {
            throw new Exception("The connection string doesn't contain a value for 'host'.");
        }

        connectionFactory.Ssl.ServerName = connectionFactory.HostName;
        connectionFactory.Ssl.Certs = null;
        connectionFactory.Ssl.Version = SslProtocols.Tls12;
        connectionFactory.Ssl.Enabled = connectionStringParser.IsTls;

        using (var connection = connectionFactory.CreateConnection("Test Queue Purger"))
        using (var channel = connection.CreateModel())
        {
            foreach (var queue in queues)
            {
                try
                {
                    channel.QueuePurge(queue);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Unable to clear queue {0}: {1}", queue, ex);
                }
            }
        }
    }

    string[] queuesToCleanUp;
    RabbitMqConnectionStringParser connectionStringBuilder;
}