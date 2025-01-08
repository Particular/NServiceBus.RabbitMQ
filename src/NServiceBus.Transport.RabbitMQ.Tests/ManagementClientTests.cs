#nullable enable

namespace NServiceBus.Transport.RabbitMQ.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using NServiceBus.Transport.RabbitMQ.ManagementClient;
    using NUnit.Framework;
    using NUnit.Framework.Internal;
    using ConnectionFactory = ConnectionFactory;


    [TestFixture]
    class ManagementClientTests
    {
        static readonly string connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString") ?? "host=localhost";
        static readonly ConnectionConfiguration connectionConfiguration = ConnectionConfiguration.Create(connectionString);
        static readonly ConnectionConfiguration managementConnectionConfiguration = ConnectionConfiguration.Create(connectionString);
        static readonly ConnectionFactory connectionFactory = new(typeof(ManagementClientTests).FullName, connectionConfiguration, null, false, false, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(10), []);
        static readonly ManagementClient client = new(managementConnectionConfiguration);

        const int defaultBrokerPort = 5672;
        const int defaultBrokerTlsPort = 5671;
        const int defaultManagementPort = 15672;
        const int defaultManagementTlsPort = 15671;
        const string defaultUserName = "guest";
        const string defaultPassword = "guest";

        static IEnumerable<TestCaseData> ConnectionConfigurationTestCases()
        {
            yield return new TestCaseData(
                $"Host=wronghost1;VirtualHost=/;Port={defaultBrokerPort};UserName={defaultUserName};Password={defaultPassword};UseTls=False",
                defaultManagementPort,
                defaultUserName,
                defaultPassword);
            yield return new TestCaseData(
                $"Host=wronghost2;VirtualHost=/;Port={defaultBrokerTlsPort};UserName={defaultUserName};Password={defaultPassword};UseTls=True",
                defaultManagementTlsPort,
                defaultUserName,
                defaultPassword);
            yield return new TestCaseData(
                $"Host=wronghost3;VirtualHost=/;Port=12345;UserName={defaultUserName};Password={defaultPassword};UseTls=False",
                12345,
                defaultUserName,
                defaultPassword);
            yield return new TestCaseData(
                $"Host=wronghost4;VirtualHost=/;Port={defaultBrokerPort};UserName=fakeUser;Password=fakePassword;UseTls=True",
                defaultManagementPort,
                "fakeUser",
                "fakePassword");
            yield return new TestCaseData(
                $"Host=wronghost5;VirtualHost=/;Port={defaultBrokerPort};UseTls=True",
                defaultManagementPort,
                defaultUserName,
                defaultPassword);
        }
        [Test, TestCaseSource(nameof(ConnectionConfigurationTestCases))]
        public void ValidateConnectionConfiguration_Should_Set_Default_Port_And_Authorization_Configurations(string connectionString, int expectedPort, string expectedUserName, string expectedPassword)
        {
            var connectionConfiguration = ConnectionConfiguration.Create(connectionString);
            var client = new ManagementClient(connectionConfiguration);
            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await client.ValidateConnectionConfiguration());

            Assert.Multiple(() =>
            {
                Assert.That(exception!.Message, Is.EqualTo("The management connection configuration could not be validated with the supplied connection string or the default values."));
                Assert.That(client.Port, Is.EqualTo(expectedPort));
                Assert.That(client.UserName, Is.EqualTo(expectedUserName));
                Assert.That(client.Password, Is.EqualTo(expectedPassword));
            });
        }

        [Test]
        public async Task GetQueue_Should_Return_Queue_Information_When_Exists()
        {
            // Arrange
            var queueName = nameof(GetQueue_Should_Return_Queue_Information_When_Exists);
            await CreateQuorumQueue(queueName).ConfigureAwait(false);

            // Act
            var response = await client.GetQueue(queueName);

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
            var response = await client.GetOverview();

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
            var response = await client.GetFeatureFlags();

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
            var queueName = nameof(CreatePolicy_With_DeliveryLimit_Should_Be_Applied_To_Quorum_Queues);
            var policyName = $"{queueName} policy";
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
            await client.CreatePolicy(policy);

            // Assert

            // It can take some time for updated policies to be applied, so we need to wait.
            // If this test is randomly failing, consider increasing the delay
            await Task.Delay(10000);
            var response = await client.GetQueue(queueName);
            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(response.Value, Is.Not.Null);
                Assert.That(response.Value?.AppliedPolicyName, Is.EqualTo(policyName));
                Assert.That(response.Value?.EffectivePolicyDefinition?.DeliveryLimit, Is.EqualTo(deliveryLimit));
            });
        }

        static async Task CreateQuorumQueue(string queueName)
        {
            using var connection = await connectionFactory.CreateConnection($"{queueName} connection").ConfigureAwait(false);
            using var channel = await connection.CreateChannelAsync().ConfigureAwait(false);
            var arguments = new Dictionary<string, object?> { { "x-queue-type", "quorum" } };

            _ = await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: arguments);
        }
    }
}
