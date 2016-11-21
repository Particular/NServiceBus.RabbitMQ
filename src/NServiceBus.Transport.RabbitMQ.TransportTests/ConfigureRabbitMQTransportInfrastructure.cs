using System;
using System.Data.Common;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Settings;
using NServiceBus.TransportTests;
using NServiceBus.Transport;
using RabbitMQ.Client;

class ConfigureRabbitMQTransportInfrastructure : IConfigureTransportInfrastructure
{
    public TransportConfigurationResult Configure(SettingsHolder settings, TransportTransactionMode transactionMode)
    {
        var result = new TransportConfigurationResult();
        var transport = new RabbitMQTransport();

        connectionStringBuilder = new DbConnectionStringBuilder
        {
            ConnectionString = Environment.GetEnvironmentVariable("RabbitMQTransport.ConnectionString")
        };

        ApplyDefault(connectionStringBuilder, "username", "guest");
        ApplyDefault(connectionStringBuilder, "password", "guest");
        ApplyDefault(connectionStringBuilder, "virtualhost", "nsb-rabbitmq-test");
        ApplyDefault(connectionStringBuilder, "host", "localhost");

        queueBindings = settings.Get<QueueBindings>();

        result.TransportInfrastructure = transport.Initialize(settings, connectionStringBuilder.ConnectionString);
        result.PurgeInputQueueOnStartup = true;

        transportTransactionMode = result.TransportInfrastructure.TransactionMode;
        requestedTransactionMode = transactionMode;

        return result;
    }

    static void ApplyDefault(DbConnectionStringBuilder builder, string key, string value)
    {
        if (!builder.ContainsKey(key))
        {
            builder.Add(key, value);
        }
    }

    public Task Cleanup()
    {
        if (transportTransactionMode >= requestedTransactionMode)
        {
            PurgeQueues();
        }

        return Task.FromResult(0);
    }

    void PurgeQueues()
    {
        var connectionFactory = CreateConnectionFactory();

        using (var connection = connectionFactory.CreateConnection("Test Queue Purger"))
        using (var channel = connection.CreateModel())
        {
            foreach (var queue in queueBindings.ReceivingAddresses)
            {
                PurgeQueue(channel, queue);
            }

            foreach (var queue in queueBindings.SendingAddresses)
            {
                PurgeQueue(channel, queue);
            }
        }
    }

    static void PurgeQueue(IModel channel, string queue)
    {
        try
        {
            channel.QueuePurge(queue);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unable to clear queue '{queue}': {ex}");
        }
    }

    ConnectionFactory CreateConnectionFactory()
    {
        var connectionFactory = new ConnectionFactory
        {
            UserName = connectionStringBuilder["username"].ToString(),
            Password = connectionStringBuilder["password"].ToString(),
            VirtualHost = connectionStringBuilder["virtualhost"].ToString(),
            HostName = connectionStringBuilder["host"].ToString(),
            AutomaticRecoveryEnabled = true,
            UseBackgroundThreadsForIO = true
        };

        return connectionFactory;
    }

    DbConnectionStringBuilder connectionStringBuilder;
    QueueBindings queueBindings;
    TransportTransactionMode transportTransactionMode;
    TransportTransactionMode requestedTransactionMode;
}

