namespace NServiceBus.Transport.RabbitMQ.Tests
{
    using System;
    using System.Net.Http;
    using NServiceBus.Transport.RabbitMQ.Tests.ConnectionString;
    using NUnit.Framework;

    [TestFixture]
    class InitializeTransportTests
    {
        static readonly string BrokerConnectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString") ?? "host=localhost";
        static HostSettings HostSettings { get; } = new(nameof(ConnectionConfigurationTests), nameof(ConnectionConfigurationTests), null, null, false);

        [Test]
        public void Should_not_throw_with_valid_legacy_management_url()
        {
            var broker = ConnectionConfiguration.Create(BrokerConnectionString);
            // Create transport in legacy mode
            var transport = new RabbitMQTransport
            {
                TopologyFactory = durable => new ConventionalRoutingTopology(durable, QueueType.Quorum),
                LegacyApiConnectionString = BrokerConnectionString,
                LegacyManagementApiUrl = $"{(broker.UseTls ? "https" : "http")}://{broker.UserName}:{broker.Password}@{broker.Host}:{(broker.UseTls ? "15671" : "15672")}",
                DoNotUseManagementClient = false
            };

            Assert.DoesNotThrowAsync(async () => await transport.Initialize(HostSettings, [], []));
        }

        [Test]
        public void Should_throw_on_invalid_management_scheme()
        {
            var broker = ConnectionConfiguration.Create(BrokerConnectionString);
            // Create transport in legacy mode
            var transport = new RabbitMQTransport
            {
                TopologyFactory = durable => new ConventionalRoutingTopology(durable, QueueType.Quorum),
                LegacyApiConnectionString = BrokerConnectionString,
                LegacyManagementApiUrl = $"{(broker.UseTls ? "http" : "https")}://guest:guest@{broker.Host}:{(broker.UseTls ? "15671" : "15672")}",
                DoNotUseManagementClient = false
            };

            _ = Assert.ThrowsAsync<HttpRequestException>(async () => await transport.Initialize(HostSettings, [], []));
        }

        [Test]
        public void Should_throw_on_invalid_legacy_management_credentials()
        {
            var broker = ConnectionConfiguration.Create(BrokerConnectionString);
            // Create transport in legacy mode
            var transport = new RabbitMQTransport
            {
                TopologyFactory = durable => new ConventionalRoutingTopology(durable, QueueType.Quorum),
                LegacyApiConnectionString = BrokerConnectionString,
                LegacyManagementApiUrl = $"{(broker.UseTls ? "https" : "http")}://copa:abc123xyz@{broker.Host}:{(broker.UseTls ? "15671" : "15672")}",
                DoNotUseManagementClient = false
            };

            _ = Assert.ThrowsAsync<InvalidOperationException>(async () => await transport.Initialize(HostSettings, [], []));
        }

        [Test]
        public void Should_throw_on_invalid_management_host()
        {
            var broker = ConnectionConfiguration.Create(BrokerConnectionString);
            // Create transport in legacy mode
            var transport = new RabbitMQTransport
            {
                TopologyFactory = durable => new ConventionalRoutingTopology(durable, QueueType.Quorum),
                LegacyApiConnectionString = BrokerConnectionString,
                LegacyManagementApiUrl = $"{(broker.UseTls ? "https" : "http")}://guest:guest@wronghost:{(broker.UseTls ? "15671" : "15672")}",
                DoNotUseManagementClient = false
            };

            _ = Assert.ThrowsAsync<HttpRequestException>(async () => await transport.Initialize(HostSettings, [], []));
        }

        [Test]
        public void Should_throw_on_invalid_management_port()
        {
            var broker = ConnectionConfiguration.Create(BrokerConnectionString);
            // Create transport in legacy mode
            var transport = new RabbitMQTransport
            {
                TopologyFactory = durable => new ConventionalRoutingTopology(durable, QueueType.Quorum),
                LegacyApiConnectionString = BrokerConnectionString,
                LegacyManagementApiUrl = $"{(broker.UseTls ? "https" : "http")}://guest:guest@{broker.Host}:12345",
                DoNotUseManagementClient = false
            };

            _ = Assert.ThrowsAsync<HttpRequestException>(async () => await transport.Initialize(HostSettings, [], []));
        }

        string CreateLegacyManagementApiUrl(
            bool useTls,
            string host,
            string userName,
            string password,
            string port)
        {
            var scheme = useTls ? "https" : "http";
            return $"{scheme}://{userName}:{password}@{host}:{port}";
        }

        static RabbitMQTransport CreateTransport(string managementApiUrl) => new()
        {
            TopologyFactory = durable => new ConventionalRoutingTopology(durable, QueueType.Quorum),
            LegacyApiConnectionString = BrokerConnectionString,
            LegacyManagementApiUrl = managementApiUrl,
            DoNotUseManagementClient = false
        };
    }
}
