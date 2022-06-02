using System;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Settings;
using NServiceBus.Transport;
using NServiceBus.Transport.RabbitMQ;
using NServiceBus.TransportTests;
using RabbitMQ.Client;

class ConfigureRabbitMQTransportInfrastructure : IConfigureTransportInfrastructure
{
    public TransportConfigurationResult Configure(SettingsHolder settings, TransportTransactionMode transactionMode)
    {
        var result = new TransportConfigurationResult();
        var transport = new RabbitMQTransport();

        var connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new Exception("The 'RabbitMQTransport_ConnectionString' environment variable is not set.");
        }

        connectionConfiguration = ConnectionConfiguration.Create(connectionString, string.Empty);

        queueBindings = settings.Get<QueueBindings>();

        new TransportExtensions<RabbitMQTransport>(settings).UseConventionalRoutingTopology(QueueType.Classic);
        result.TransportInfrastructure = transport.Initialize(settings, connectionString);
        isTransportInitialized = true;
        result.PurgeInputQueueOnStartup = true;

        transportTransactionMode = result.TransportInfrastructure.TransactionMode;
        requestedTransactionMode = transactionMode;

        return result;
    }

    public Task Cleanup()
    {
        if (isTransportInitialized && transportTransactionMode >= requestedTransactionMode)
        {
            PurgeQueues(connectionConfiguration, queueBindings);
        }

        return Task.FromResult(0);
    }

    static void PurgeQueues(ConnectionConfiguration connectionConfiguration, QueueBindings queueBindings)
    {
        if (connectionConfiguration == null)
        {
            return;
        }

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

        var queues = queueBindings.ReceivingAddresses.Concat(queueBindings.SendingAddresses);

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

    ConnectionConfiguration connectionConfiguration;
    QueueBindings queueBindings;
    TransportTransactionMode transportTransactionMode;
    TransportTransactionMode requestedTransactionMode;
    bool isTransportInitialized;
}

