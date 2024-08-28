using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Transport;
using NServiceBus.Transport.RabbitMQ.AcceptanceTests;

class ConfigureEndpointRabbitMQTransport : IConfigureEndpointTestExecution
{
    TestRabbitMQTransport transport;
    readonly QueueType queueType;

    public ConfigureEndpointRabbitMQTransport(QueueType queueType = QueueType.Classic)
    {
        this.queueType = queueType;
    }

    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        transport = new TestRabbitMQTransport(RoutingTopology.Conventional(queueType, type => type.FullName), ConnectionHelper.ConnectionString);

        configuration.UseTransport(transport);

        return Task.CompletedTask;
    }

    public async Task Cleanup()
    {
        await PurgeQueues();
    }

    async Task PurgeQueues()
    {
        if (transport == null)
        {
            return;
        }

        var queues = transport.QueuesToCleanup.Distinct().ToArray();

        using (var connection = await ConnectionHelper.ConnectionFactory.CreateConnectionAsync("Test Queue Purger"))
        using (var channel = await connection.CreateChannelAsync())
        {
            foreach (var queue in queues)
            {
                try
                {
                    await channel.QueuePurgeAsync(queue);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Unable to clear queue {0}: {1}", queue, ex);
                }
            }
        }
    }

    class TestRabbitMQTransport : RabbitMQTransport
    {
        public TestRabbitMQTransport(RoutingTopology routingTopology, string connectionString)
            : base(routingTopology, connectionString)
        {
        }

        public override async Task<TransportInfrastructure> Initialize(HostSettings hostSettings, ReceiveSettings[] receivers, string[] sendingAddresses, CancellationToken cancellationToken = default)
        {
            var infrastructure = await base.Initialize(hostSettings, receivers, sendingAddresses, cancellationToken);
            QueuesToCleanup.AddRange(infrastructure.Receivers.Select(x => x.Value.ReceiveAddress).Concat(sendingAddresses).Distinct());
            return infrastructure;
        }

        public List<string> QueuesToCleanup { get; } = [];
    }
}