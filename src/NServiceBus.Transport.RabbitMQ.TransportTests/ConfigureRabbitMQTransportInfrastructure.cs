using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Transport;
using NServiceBus.TransportTests;
using RabbitMQ.Client;

class ConfigureRabbitMQTransportInfrastructure : IConfigureTransportInfrastructure
{
    public TransportDefinition CreateTransportDefinition()
    {
        var connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new Exception("The 'RabbitMQTransport_ConnectionString' environment variable is not set.");
        }

        connectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = connectionString };

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

    static void PurgeQueues(DbConnectionStringBuilder connectionStringBuilder, string[] queues)
    {
        if (connectionStringBuilder == null || queues == null)
        {
            return;
        }

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
    DbConnectionStringBuilder connectionStringBuilder;
}