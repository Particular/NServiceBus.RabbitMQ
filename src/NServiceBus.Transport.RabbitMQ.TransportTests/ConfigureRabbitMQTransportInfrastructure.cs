using System;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Transport;
using NServiceBus.TransportTests;

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
        if (queuesToCleanUp == null)
        {
            return Task.CompletedTask;
        }

        using (var connection = ConnectionHelper.ConnectionFactory.CreateConnection("Test Queue Purger"))
        using (var channel = connection.CreateModel())
        {
            foreach (var queue in queuesToCleanUp)
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
        return Task.CompletedTask;
    }

    string[] queuesToCleanUp;
}