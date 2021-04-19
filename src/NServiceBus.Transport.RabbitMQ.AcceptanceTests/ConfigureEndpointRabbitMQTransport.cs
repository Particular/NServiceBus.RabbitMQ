using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Transport;
using NServiceBus.Transport.RabbitMQ;
using NServiceBus.Transport.RabbitMQ.AcceptanceTests;

class ConfigureEndpointRabbitMQTransport : IConfigureEndpointTestExecution
{
    RabbitMqConnectionStringParser connectionStringParser;
    TestRabbitMQTransport transport;
    readonly QueueMode queueMode;

    public ConfigureEndpointRabbitMQTransport(QueueMode queueMode = QueueMode.Classic)
    {
        this.queueMode = queueMode;
    }

    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        transport = new TestRabbitMQTransport(
            new ConventionalRoutingTopology(true, type => type.FullName),
            ConnectionHelper.ConnectionString,
            queueMode);

        var connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new Exception("The 'RabbitMQTransport_ConnectionString' environment variable is not set.");
        }

        //For cleanup
        connectionStringParser = new RabbitMqConnectionStringParser(connectionString);

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
        if (connectionStringParser == null)
        {
            return;
        }

        var connectionFactory = new ConnectionFactory
        {
            AutomaticRecoveryEnabled = true,
            UseBackgroundThreadsForIO = true
        };

        connectionFactory.UserName = connectionStringParser.UserName;
        connectionFactory.Password = connectionStringParser.Password;
        connectionFactory.HostName = connectionStringParser.HostName;
        connectionFactory.VirtualHost = "/";

        if (connectionStringParser.Port > 0)
        {
            connectionFactory.Port = connectionStringParser.Port;
        }

        if (!string.IsNullOrWhiteSpace(connectionStringParser.VirtualHost))
        {
            connectionFactory.VirtualHost = connectionStringParser.VirtualHost;
        }

        if (string.IsNullOrWhiteSpace(connectionFactory.HostName))
        {
            throw new Exception("The connection string doesn't contain a value for 'host'.");
        }

        connectionFactory.Ssl.ServerName = connectionFactory.HostName;
        connectionFactory.Ssl.Certs = null;
        connectionFactory.Ssl.Version = SslProtocols.Tls12;
        connectionFactory.Ssl.Enabled = connectionStringParser.IsTls;

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
        public TestRabbitMQTransport(IRoutingTopology topology, string connectionString, QueueMode queueMode = QueueMode.Classic)
            : base(topology, connectionString, queueMode)
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