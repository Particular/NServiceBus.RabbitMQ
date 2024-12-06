#nullable enable

namespace NServiceBus.Transport.RabbitMQ.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using NServiceBus.Transport.RabbitMQ.Administration.ManagementClient;
    using NServiceBus.Transport.RabbitMQ.Administration.ManagementClient.Models;
    using NUnit.Framework;
    using NUnit.Framework.Internal;
    using ConnectionFactory = ConnectionFactory;


    [TestFixture]
    class ManagementClientTests
    {
        static readonly string connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString") ?? "host=localhost";
        static readonly ConnectionConfiguration connectionConfiguration = ConnectionConfiguration.Create(connectionString);
        static readonly ConnectionConfiguration managementConnectionConfiguration = ConnectionConfiguration.Create(connectionString, isManagementConnection: true);
        static readonly ConnectionFactory connectionFactory = new(typeof(ManagementClientTests).FullName, connectionConfiguration, null, false, false, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(10), []);
        static readonly ManagementClient client = new(managementConnectionConfiguration);

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
