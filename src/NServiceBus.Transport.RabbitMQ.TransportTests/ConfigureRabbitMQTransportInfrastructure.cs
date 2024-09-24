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

        var transport = new RabbitMQTransport(RoutingTopology.Conventional(QueueType.Classic), connectionString, false);

        return transport;
    }

    public async Task<TransportInfrastructure> Configure(TransportDefinition transportDefinition, HostSettings hostSettings, QueueAddress inputQueue, string errorQueueName, CancellationToken cancellationToken = default)
    {
        var mainReceiverSettings = new ReceiveSettings(
            "mainReceiver",
            inputQueue,
            true,
            false,
            errorQueueName);

        var transport = await transportDefinition.Initialize(hostSettings, [mainReceiverSettings], [errorQueueName], cancellationToken);

        queuesToCleanUp = [transport.ToTransportAddress(inputQueue), errorQueueName];
        return transport;
    }

    public async Task Cleanup(CancellationToken cancellationToken = default)
    {
        if (queuesToCleanUp == null)
        {
            return;
        }

        using var connection = await ConnectionHelper.ConnectionFactory.CreateConnectionAsync("Test Queue Purger", cancellationToken);
        using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        foreach (var queue in queuesToCleanUp)
        {
            try
            {
                await channel.QueuePurgeAsync(queue, cancellationToken);
            }
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
            {
                Console.WriteLine("Unable to clear queue {0}: {1}", queue, ex);
            }
        }
    }

    string[] queuesToCleanUp;
}