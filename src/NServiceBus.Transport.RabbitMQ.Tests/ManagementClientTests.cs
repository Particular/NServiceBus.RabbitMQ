﻿#nullable enable

namespace NServiceBus.Transport.RabbitMQ.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using NServiceBus.Transport.RabbitMQ.ManagementApi;
    using NUnit.Framework;
    using NUnit.Framework.Internal;
    using RabbitMQClient = global::RabbitMQ.Client;

    [TestFixture]
    class ManagementClientTests
    {
        static readonly string connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString") ?? "host=localhost";
        static readonly ConnectionConfiguration connectionConfiguration = ConnectionConfiguration.Create(connectionString);
        static readonly ConnectionFactory connectionFactory = new(typeof(ManagementClientTests).FullName, connectionConfiguration, null, false, false, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(10), []);

        [Test]
        public void Should_Throw_With_Invalid_Scheme()
        {
            var managementApiConfiguration = new ManagementApiConfiguration("amqp://localhost:15672");

            var exception = Assert.Throws<NotSupportedException>(() => new ManagementClient(connectionConfiguration, managementApiConfiguration));
        }

        [Test]
        public async Task GetQueue_Should_Return_Queue_Information()
        {
            var managementClient = new ManagementClient(connectionConfiguration);
            var queueName = nameof(GetQueue_Should_Return_Queue_Information);
            await CreateQuorumQueue(queueName).ConfigureAwait(false);

            var response = await managementClient.GetQueue(queueName);

            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(response.Value?.Name, Is.EqualTo(queueName));
            });
        }

        [Test]
        public async Task GetOverview_Should_Return_Broker_Information()
        {
            var managementClient = new ManagementClient(connectionConfiguration);

            var response = await managementClient.GetOverview();

            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(response.Value?.BrokerVersion, Is.Not.Null);
            });
        }

        [Test]
        public async Task GetFeatureFlags_Should_Return_FeatureFlag_Information()
        {
            var managementClient = new ManagementClient(connectionConfiguration);

            var response = await managementClient.GetFeatureFlags();

            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(response.Value, Is.Not.Empty);
            });
        }

        [Test]
        public async Task GetBindingsForQueue_Should_Return_List_Of_Bindings_On_A_Queue()
        {
            var managementClient = new ManagementClient(connectionConfiguration);
            var queueName = nameof(GetBindingsForQueue_Should_Return_List_Of_Bindings_On_A_Queue);

            await CreateExchangeAndBindQueue(queueName, "topic-exchange", $"#{queueName}");

            var response = await managementClient.GetBindingsForQueue(queueName);

            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(response.Value, Has.Count.EqualTo(2));
            });
        }

        [Test]
        public async Task GetBindingsForExchange_Should_Return_List_Of_Bindings_Where_The_Exchange_Is_The_Destination()
        {
            var managementClient = new ManagementClient(connectionConfiguration);
            var sourceExchangeName = "GetExchangeBindings-source";
            var destinationExchangeName = "GetExchangeBindings-destination";

            await CreateExchangeToExchangeBinding(sourceExchangeName, destinationExchangeName, $"#{destinationExchangeName}");

            var response = await managementClient.GetBindingsForExchange(destinationExchangeName);

            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(response.Value, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task CreatePolicy_Should_Create_Policy()
        {
            var managementClient = new ManagementClient(connectionConfiguration);
            var policyName = $"test-management-client-create-policy";

            var policy = new Policy
            {
                ApplyTo = PolicyTarget.QuorumQueues,
                Definition = new PolicyDefinition
                {
                    DeliveryLimit = 100
                },
                Pattern = policyName,
                Priority = 100
            };

            await managementClient.CreatePolicy(policyName, policy);
        }

        static async Task CreateQuorumQueue(string queueName)
        {
            using var connection = await connectionFactory.CreateAdministrationConnection().ConfigureAwait(false);
            using var channel = await connection.CreateChannelAsync().ConfigureAwait(false);

            var arguments = new Dictionary<string, object?> { { "x-queue-type", "quorum" } };

            _ = await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: arguments);
        }

        static async Task CreateExchangeAndBindQueue(string queueName, string exchangeName, string routingKey)
        {
            using var connection = await connectionFactory.CreateAdministrationConnection().ConfigureAwait(false);
            using var channel = await connection.CreateChannelAsync().ConfigureAwait(false);

            var arguments = new Dictionary<string, object?> { { "x-queue-type", "quorum" } };

            _ = await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: arguments);

            await channel.ExchangeDeclareAsync(exchangeName, RabbitMQClient.ExchangeType.Topic, durable: true, autoDelete: false).ConfigureAwait(false);
            await channel.QueueBindAsync(queueName, exchangeName, routingKey).ConfigureAwait(false);
        }

        static async Task CreateExchangeToExchangeBinding(string sourceExchange, string destinationExchange, string routingKey)
        {
            using var connection = await connectionFactory.CreateAdministrationConnection().ConfigureAwait(false);
            using var channel = await connection.CreateChannelAsync().ConfigureAwait(false);

            await channel.ExchangeDeclareAsync(sourceExchange, RabbitMQClient.ExchangeType.Topic, durable: true, autoDelete: false).ConfigureAwait(false);
            await channel.ExchangeDeclareAsync(destinationExchange, RabbitMQClient.ExchangeType.Topic, durable: true, autoDelete: false).ConfigureAwait(false);
            await channel.ExchangeBindAsync(destinationExchange, sourceExchange, routingKey).ConfigureAwait(false);
        }
    }
}
