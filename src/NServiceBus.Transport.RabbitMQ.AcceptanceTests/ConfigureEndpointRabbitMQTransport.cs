using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Transport;
using NServiceBus.Transport.RabbitMQ;
using NServiceBus.Transport.RabbitMQ.AcceptanceTests;

class ConfigureEndpointRabbitMQTransport : IConfigureEndpointTestExecution
{
    TestRabbitMQTransport transport;
    readonly QueueMode queueMode;
    readonly bool enableTimeouts;

    public ConfigureEndpointRabbitMQTransport(QueueMode queueMode = QueueMode.Classic, bool enableTimeouts = true)
    {
        this.queueMode = queueMode;
        this.enableTimeouts = enableTimeouts;
    }

    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        transport = new TestRabbitMQTransport(
            new ConventionalRoutingTopology(true, type => type.FullName),
            ConnectionHelper.ConnectionString,
            queueMode,
            enableTimeouts);

        configuration.UseTransport(transport);

        return Task.CompletedTask;
    }

    public Task Cleanup()
    {
        PurgeQueues();

        return Task.CompletedTask;
    }

    void PurgeQueues()
    {
        if (transport == null)
        {
            return;
        }

        var queues = transport.QueuesToCleanup.Distinct().ToArray();

        using (var connection = ConnectionHelper.ConnectionFactory.CreateConnection("Test Queue Purger"))
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

    class TestRabbitMQTransport : RabbitMQTransport
    {
        public TestRabbitMQTransport(IRoutingTopology topology, string connectionString, QueueMode queueMode, bool enableTimeouts)
            : base(topology, connectionString, queueMode, enableTimeouts)
        {
        }

        public override async Task<TransportInfrastructure> Initialize(HostSettings hostSettings, ReceiveSettings[] receivers, string[] sendingAddresses, CancellationToken cancellationToken = default)
        {
            var infrastructure = await base.Initialize(hostSettings, receivers, sendingAddresses, cancellationToken);
            QueuesToCleanup.AddRange(infrastructure.Receivers.Select(x => x.Value.ReceiveAddress).Concat(sendingAddresses).Distinct());
            return infrastructure;
        }

        public List<string> QueuesToCleanup { get; } = new List<string>();
    }
}