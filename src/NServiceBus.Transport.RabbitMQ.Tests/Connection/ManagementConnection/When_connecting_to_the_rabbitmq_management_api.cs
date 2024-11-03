namespace NServiceBus.Transport.RabbitMQ.Tests.Connection.ManagementConnection
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using NServiceBus.Transport.RabbitMQ.Administration.ManagementClient;
    using NUnit.Framework;
    using System.Threading;

    [TestFixture]
    class When_connecting_to_the_rabbitmq_management_api : RabbitMQTransport
    {
        ConnectionConfiguration connectionConfiguration = ConnectionConfiguration.Create("host=localhost");
        protected IList<string> AdditionalReceiverQueues = [];
        protected QueueType queueType = QueueType.Quorum;
        protected string ReceiverQueue => GetTestQueueName("ManagementAPITestQueue");
        protected string ErrorQueue => GetTestQueueName("error");
        protected string GetTestQueueName(string queueName) => $"{queueName}-{queueType}";
        readonly List<(string hostName, int port, bool useTls)> additionalClusterNodes = [];
        internal new IRoutingTopology RoutingTopology;
        internal RabbitMQTransport transport;

        [SetUp]
        public async Task SetUp()
        {
            var connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString") ?? "host=localhost";

            var useTls = connectionString.StartsWith("https", StringComparison.InvariantCultureIgnoreCase) || connectionString.StartsWith("amqps", StringComparison.InvariantCultureIgnoreCase);

            connectionConfiguration = ConnectionConfiguration.Create(connectionString);
            RoutingTopology = NServiceBus.RoutingTopology.Conventional(queueType).Create();

            transport = new RabbitMQTransport(NServiceBus.RoutingTopology.Conventional(queueType), connectionString);

            _ = await Initialize(new HostSettings(ReceiverQueue, ReceiverQueue, new StartupDiagnosticEntries(), (_, _, _) => { }, true),
                [new ReceiveSettings(ReceiverQueue, new QueueAddress(ReceiverQueue), true, true, ErrorQueue)], [.. AdditionalReceiverQueues, ErrorQueue]);
        }

        public override async Task<TransportInfrastructure> Initialize(HostSettings hostSettings, ReceiveSettings[] receivers, string[] sendingAddresses, CancellationToken cancellationToken = default)
        {
            transport.ValidateAndApplyLegacyConfiguration();

            X509Certificate2Collection certCollection = null;

            if (ClientCertificate != null)
            {
                certCollection = new X509Certificate2Collection(ClientCertificate);
            }

            var connectionFactory = new ConnectionFactory(hostSettings.Name, connectionConfiguration, certCollection, !ValidateRemoteCertificate,
                UseExternalAuthMechanism, HeartbeatInterval, NetworkRecoveryInterval, additionalClusterNodes);

            //var managementClient = new ManagementClient(ConnectionConfiguration);

            var channelProvider = new ChannelProvider(connectionFactory, NetworkRecoveryInterval, RoutingTopology);
            await channelProvider.CreateConnection(cancellationToken).ConfigureAwait(false);

            var converter = new MessageConverter(MessageIdStrategy);

            var infra = new RabbitMQTransportInfrastructure(hostSettings, receivers, connectionFactory,
                RoutingTopology, channelProvider, converter, null, TimeToWaitBeforeTriggeringCircuitBreaker,
                PrefetchCountCalculation, NetworkRecoveryInterval, SupportsDelayedDelivery);

            if (hostSettings.SetupInfrastructure)
            {
                await infra.SetupInfrastructure(sendingAddresses, cancellationToken).ConfigureAwait(false);
            }

            return infra;
        }

        [Test]
        public async Task GetQueue_Should_Return_Queue_When_Exists()
        {
            var client = new ManagementClient(connectionConfiguration);

            var response = await client.GetQueue(ReceiverQueue);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(response.Value, Is.Not.Null);
                Assert.That(response.Value?.Name, Is.EqualTo(ReceiverQueue));
            });
        }
    }
}
