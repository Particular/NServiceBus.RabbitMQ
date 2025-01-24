#nullable enable

namespace NServiceBus.Transport.RabbitMQ.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using NServiceBus.Transport.RabbitMQ.ManagementApi;
    using NUnit.Framework;
    using NUnit.Framework.Internal;
    using static NServiceBus.Transport.RabbitMQ.Tests.FakeHttpClient;

    [TestFixture]
    class ManagementClientTests
    {
        static readonly string connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString") ?? "host=localhost";
        static readonly ConnectionConfiguration connectionConfiguration = ConnectionConfiguration.Create(connectionString);
        static readonly ConnectionFactory connectionFactory = new(typeof(ManagementClientTests).FullName, connectionConfiguration, null, false, false, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(10), []);
        string defaultManagementUrl;
        ManagementClient? managementClient;

        const int defaultBrokerPort = 5672;
        const int defaultBrokerTlsPort = 5671;
        const int defaultManagementPort = 15672;
        const int defaultManagementTlsPort = 15671;
        const string defaultUserName = "guest";
        const string defaultPassword = "guest";
        const string defaultVirtualHost = "/";

        [SetUp]
        public void SetUp() => defaultManagementUrl = ManagementClient.CreateManagementConnectionString(connectionConfiguration);

        [Test]
        [TestCase("http://localhost", "guest", "guest", "http://localhost:15672")]
        [TestCase("https://localhost", "guest", "guest", "https://localhost:15671")]
        [TestCase("http://localhost:15672", "guest", "guest", "http://localhost:15672")]
        [TestCase("https://localhost:15671", "guest", "guest", "https://localhost:15671")]
        [TestCase("http://guest:guest@localhost", "guest", "guest", "http://localhost:15672")]
        [TestCase("https://guest:guest@localhost", "guest", "guest", "https://localhost:15671")]
        [TestCase("http://guest:guest@localhost:15672", "guest", "guest", "http://localhost:15672")]
        [TestCase("https://guest:guest@localhost:15671", "guest", "guest", "https://localhost:15671")]
        public async Task GetOverview_Should_Return_Success_With_Valid_Default_Connection_Values(
            string managementApiUrl,
            string expectedUserName,
            string expectedPassword,
            string expectedUrl)
        {
            var HttpClient = CreateFakeHttpClient(request => FakeResponses.GetOverview(request, expectedUserName, expectedPassword, expectedUrl));
            managementClient = CreateManagementClient(managementApiUrl, HttpClient);

            var result = await managementClient.GetOverview();

            Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        [TestCase("http://localhost", "user", "password", "http://localhost:15672")]
        [TestCase("https://localhost", "user", "password", "https://localhost:15671")]
        [TestCase("http://localhost:15672", "user", "password", "http://localhost:15672")]
        [TestCase("https://localhost:15671", "user", "password", "https://localhost:15671")]
        [TestCase("http://guest:guest@localhost", "user", "password", "http://localhost:15672")]
        [TestCase("https://guest:guest@localhost", "user", "password", "https://localhost:15671")]
        [TestCase("http://guest:guest@localhost:15672", "user", "password", "http://localhost:15672")]
        [TestCase("https://guest:guest@localhost:15671", "user", "password", "https://localhost:15671")]
        public async Task GetOverview_Should_Return_Unauthorized_With_Invalid_Credentials(
            string managementApiUrl,
            string expectedUserName,
            string expectedPassword,
            string expectedUrl)
        {
            var HttpClient = CreateFakeHttpClient(request => FakeResponses.GetOverview(request, expectedUserName, expectedPassword, expectedUrl));
            managementClient = CreateManagementClient(managementApiUrl, HttpClient);

            var result = await managementClient.GetOverview();
            Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public void Should_Throw_With_Invalid_Scheme()
        {
            var managementApiUrl = "amqp:guest:guest@localhost:15672";

            var exception = Assert.Throws<NotSupportedException>(() => managementClient = new(managementApiUrl, defaultVirtualHost));
        }

        [Test]
        public async Task GetQueue_Should_Return_Queue_Information_When_Exists()
        {
            // Arrange
            managementClient = new(defaultManagementUrl, defaultVirtualHost);
            var queueName = nameof(GetQueue_Should_Return_Queue_Information_When_Exists);
            await CreateQuorumQueue(queueName).ConfigureAwait(false);

            // Act
            var response = await managementClient.GetQueue(queueName);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(response.Value, Is.Not.Null);
                Assert.That(response.Value?.Name, Is.EqualTo(queueName));
            });
        }

        [Test]
        public async Task GetOverview_Should_Return_Broker_Information()
        {
            // Act
            managementClient = new(defaultManagementUrl, defaultVirtualHost);
            var response = await managementClient.GetOverview();

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(response.Value, Is.Not.Null);
                Assert.That(response.Value?.ProductName, Is.EqualTo("RabbitMQ"));
                Assert.That(response.Value?.ManagementVersion.Major, Is.InRange(3, 4));
                Assert.That(response.Value?.ProductVersion.Major, Is.InRange(3, 4));
                Assert.That(response.Value?.RabbitMqVersion.Major, Is.InRange(3, 4));
            });
        }

        [Test]
        public async Task GetFeatureFlags_Should_Return_FeatureFlag_Information()
        {
            // Act
            managementClient = new(defaultManagementUrl, defaultVirtualHost);
            var response = await managementClient.GetFeatureFlags();

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(response.Value, Is.Not.Null);
                Assert.That(response.Value, Is.Not.Empty);
                Assert.That(response.Value?.Contains(FeatureFlags.QuorumQueue), Is.True);
            });
        }

        [Test]
        [TestCase(-1)]
        [TestCase(200)]
        public async Task CreatePolicy_With_DeliveryLimit_Should_Be_Applied_To_Quorum_Queues(int deliveryLimit)
        {
            // Arrange
            managementClient = new(defaultManagementUrl, defaultVirtualHost);
            var queueName = nameof(CreatePolicy_With_DeliveryLimit_Should_Be_Applied_To_Quorum_Queues);
            var policyName = $"nsb.{queueName}";
            await CreateQuorumQueue(queueName);

            // Act
            var policy = new Policy
            {
                ApplyTo = PolicyTarget.QuorumQueues,
                Definition = new PolicyDefinition
                {
                    DeliveryLimit = deliveryLimit
                },
                Name = policyName,
                Pattern = queueName,
                Priority = 100
            };
            await managementClient.CreatePolicy(policy);

            // Assert

            // It can take some time for updated policies to be applied, so we need to wait.
            // If this test is randomly failing, consider increasing the maxWaitTime
            var maxWaitTime = TimeSpan.FromSeconds(30);
            var pollingInterval = TimeSpan.FromSeconds(2);
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < maxWaitTime)
            {
                var response = await managementClient.GetQueue(queueName);
                if (response.StatusCode == HttpStatusCode.OK
                    && response.Value != null
                    && response.Value.AppliedPolicyName == policyName
                    && response.Value.EffectivePolicyDefinition?.DeliveryLimit == deliveryLimit)
                {
                    // Policy applied successfully
                    return;
                }
                await Task.Delay(pollingInterval);
            }
            Assert.Fail($"Policy '{policyName}' was not applied to queue '{queueName}' within {maxWaitTime.TotalSeconds} seconds.");
        }

        static async Task CreateQuorumQueue(string queueName)
        {
            using var connection = await connectionFactory.CreateConnection($"{queueName} connection").ConfigureAwait(false);
            using var channel = await connection.CreateChannelAsync().ConfigureAwait(false);
            var arguments = new Dictionary<string, object?> { { "x-queue-type", "quorum" }, { "delivery_limit", 5 } };

            _ = await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: arguments);
        }

        static ManagementClient CreateManagementClient(string managementApiUrl, HttpClient httpClient) => new(defaultVirtualHost, httpClient, managementApiUrl);
    }
}
