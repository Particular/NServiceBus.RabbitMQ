#nullable enable

namespace NServiceBus.Transport.RabbitMQ.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using NServiceBus.Transport.RabbitMQ.Administration.ManagementClient;
    using NUnit.Framework;
    using NUnit.Framework.Internal;
    using ConnectionFactory = ConnectionFactory;


    [TestFixture]
    class ManagementClientTests
    {
        static readonly string connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString") ?? "host=localhost";
        readonly ConnectionConfiguration connectionConfiguration = ConnectionConfiguration.Create(connectionString);
        readonly ConnectionConfiguration managementConnectionConfiguration = ConnectionConfiguration.Create(connectionString, isManagementConnection: true);
        protected QueueType queueType = QueueType.Quorum;
        protected string ReceiverQueue => GetTestQueueName("ManagementAPITestQueue");
        protected string GetTestQueueName(string queueName) => $"{queueName}-{queueType}";

        [SetUp]
        public async Task SetUp()
        {
            var connectionFactory = new ConnectionFactory(ReceiverQueue, connectionConfiguration, null, false, false, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(10), []);
            IConnection connection = await connectionFactory.CreateConnection(ReceiverQueue).ConfigureAwait(false);
            var createChannelOptions = new CreateChannelOptions(publisherConfirmationsEnabled: false, publisherConfirmationTrackingEnabled: false);
            var channel = await connection.CreateChannelAsync(createChannelOptions).ConfigureAwait(false);
            var arguments = new Dictionary<string, object?> { { "x-queue-type", "quorum" } };

            _ = await channel.QueueDeclareAsync(queue: ReceiverQueue, durable: true, exclusive: false, autoDelete: false, arguments: arguments).ConfigureAwait(false);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (o, a) => await Task.Yield();

            var consumerTag = $"localhost - {ReceiverQueue}";
            _ = await channel.BasicConsumeAsync(ReceiverQueue, true, consumerTag, consumer).ConfigureAwait(false);
        }

        [Test]
        public async Task GetQueue_Should_Return_Queue_Information_When_Exists()
        {
            var client = new ManagementClient(managementConnectionConfiguration);

            var response = await client.GetQueue(ReceiverQueue);

            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(response.Value, Is.Not.Null);
                Assert.That(response.Value?.Name, Is.EqualTo(ReceiverQueue));
            });
        }

        [Test]
        public async Task GetOverview_Should_Return_Broker_Information_When_Exists()
        {
            var client = new ManagementClient(managementConnectionConfiguration);

            var response = await client.GetOverview();

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
    }
}
