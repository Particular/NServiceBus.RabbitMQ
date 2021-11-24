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
        var connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString") ?? "host=localhost";

        var transport = new RabbitMQTransport(Topology.Conventional, connectionString);

        return transport;
    }

    public async Task<TransportInfrastructure> Configure(TransportDefinition transportDefinition, HostSettings hostSettings, QueueAddress inputQueue, string errorQueueName, CancellationToken cancellationToken = default)
    {
        var mainReceiverSettings = new ReceiveSettings(
            "mainReceiver",
            inputQueue,
            true,
            true, errorQueueName);

        var transport = await transportDefinition.Initialize(hostSettings, new[] { mainReceiverSettings }, new[] { errorQueueName }, cancellationToken);

        queuesToCleanUp = new[] { transport.ToTransportAddress(inputQueue), errorQueueName };
        return transport;
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