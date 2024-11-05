#nullable enable

namespace NServiceBus.Transport.RabbitMQ.Tests.Connection.ManagementConnection
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using NServiceBus.Transport.RabbitMQ.Administration.ManagementClient;
    using NUnit.Framework;
    using NUnit.Framework.Internal;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using ConnectionFactory = ConnectionFactory;


    [TestFixture]
    class When_connecting_to_the_rabbitmq_management_api : RabbitMQTransport
    {
        ConnectionConfiguration connectionConfiguration = ConnectionConfiguration.Create("host=localhost");
        protected QueueType queueType = QueueType.Quorum;
        protected string ReceiverQueue => GetTestQueueName("ManagementAPITestQueue");
        protected string GetTestQueueName(string queueName) => $"{queueName}-{queueType}";

        [SetUp]
        public async Task SetUp()
        {
            var connectionFactory = new ConnectionFactory(ReceiverQueue, connectionConfiguration, null, !ValidateRemoteCertificate, UseExternalAuthMechanism, HeartbeatInterval, NetworkRecoveryInterval, []);
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
