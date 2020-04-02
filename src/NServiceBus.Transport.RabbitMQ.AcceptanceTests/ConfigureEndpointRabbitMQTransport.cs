using System;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Transport;
using NServiceBus.Transport.RabbitMQ.AcceptanceTests;
using RabbitMQ.Client;

class ConfigureEndpointRabbitMQTransport : IConfigureEndpointTestExecution
{
    QueueBindings queueBindings;

    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        var connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new Exception("The 'RabbitMQTransport_ConnectionString' environment variable is not set.");
        }

        var transport = configuration.UseTransport<RabbitMQTransport>();
        transport.ConnectionString(connectionString);
        transport.UseConventionalRoutingTopology();

        queueBindings = configuration.GetSettings().Get<QueueBindings>();

        return TaskEx.CompletedTask;
    }

    public Task Cleanup()
    {
        PurgeQueues();

        return TaskEx.CompletedTask;
    }

    void PurgeQueues()
    {
        var connectionFactory = new ConnectionFactory
        {
            AutomaticRecoveryEnabled = true,
            UseBackgroundThreadsForIO = true
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
}