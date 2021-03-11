using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Transport;
using NServiceBus.Transport.RabbitMQ;
using ConnectionFactory = RabbitMQ.Client.ConnectionFactory;

class ConfigureEndpointRabbitMQTransport : IConfigureEndpointTestExecution
{
    DbConnectionStringBuilder connectionStringBuilder;
    TestRabbitMQTransport transport;


    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        var connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new Exception("The 'RabbitMQTransport_ConnectionString' environment variable is not set.");
        }

        //For cleanup
        connectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = connectionString };

        transport = new TestRabbitMQTransport(new ConventionalRoutingTopology(true, type => type.FullName), connectionString);
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
        if (connectionStringBuilder == null || transport == null)
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

        var queues = transport.QueuesToCleanup.Distinct().ToArray();

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

    class TestRabbitMQTransport : RabbitMQTransport
    {
        public TestRabbitMQTransport(Topology topology, string connectionString)
            : base(topology, connectionString)
        {
        }

        public TestRabbitMQTransport(IRoutingTopology topology, string connectionString)
            : base(topology, connectionString)
        {
        }

        public override Task<TransportInfrastructure> Initialize(HostSettings hostSettings, ReceiveSettings[] receivers, string[] sendingAddresses, CancellationToken cancellationToken = default)
        {
            QueuesToCleanup.AddRange(receivers.Select(x => x.ReceiveAddress).Concat(sendingAddresses).Distinct());
            return base.Initialize(hostSettings, receivers, sendingAddresses, cancellationToken);
        }

        public List<string> QueuesToCleanup { get; } = new List<string>();
    }
}