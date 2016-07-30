using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Settings;
using NServiceBus.TransportTests;
using NServiceBus.Transport;
using System.Text.RegularExpressions;
using RabbitMQ.Client;

class ConfigureRabbitMQTransportInfrastructure : IConfigureTransportInfrastructure
{
    public TransportConfigurationResult Configure(SettingsHolder settings, TransportTransactionMode transactionMode)
    {
        var result = new TransportConfigurationResult();
        var transport = new RabbitMQTransport();

        connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport.ConnectionString");

        if (connectionString == null)
        {
            connectionString = "host=localhost";
        }

        queueBindings = settings.Get<QueueBindings>();

        result.TransportInfrastructure = transport.Initialize(settings, connectionString);
        result.PurgeInputQueueOnStartup = true;

        transportTransactionMode = result.TransportInfrastructure.TransactionMode;
        requestedTransactionMode = transactionMode;

        return result;
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
        var match = Regex.Match(connectionString, string.Format("[^\\w]*{0}=(?<{0}>[^;]+)", "host"), RegexOptions.IgnoreCase);

        var username = match.Groups["UserName"].Success ? match.Groups["UserName"].Value : "guest";
        var password = match.Groups["Password"].Success ? match.Groups["Password"].Value : "guest";
        var host = match.Groups["host"].Success ? match.Groups["host"].Value : "localhost";
        var virtualHost = match.Groups["VirtualHost"].Success ? match.Groups["VirtualHost"].Value : "/";

        var connectionFactory = new ConnectionFactory
        {
            UserName = username,
            Password = password,
            VirtualHost = virtualHost,
            HostName = host,
            AutomaticRecoveryEnabled = true,
            UseBackgroundThreadsForIO = true
        };

        return connectionFactory;
    }

    string connectionString;
    QueueBindings queueBindings;
    TransportTransactionMode transportTransactionMode;
    TransportTransactionMode requestedTransactionMode;
}

