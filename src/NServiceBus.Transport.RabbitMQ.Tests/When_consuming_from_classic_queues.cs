namespace NServiceBus.Transport.RabbitMQ.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client.Events;
    using NUnit.Framework;

    [TestFixture]
    class When_consuming_from_classic_queues : RabbitMqContext
    {
        [SetUp]
        public override Task SetUp()
        {
            queueType = QueueType.Classic;
            return base.SetUp();
        }

        [Test]
        public async Task Header_collection_is_null_for_headerless_messages()
        {
            var message = new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), new byte[0]);
            var headerCollectionWasNull = new TaskCompletionSource<bool>();

            OnMessage = (mc, __) =>
            {
                var basicDeliverEventArgs = mc.Extensions.Get<BasicDeliverEventArgs>();
                headerCollectionWasNull.SetResult(basicDeliverEventArgs.BasicProperties.Headers == null);

                return Task.CompletedTask;
            };

            using (var connection = connectionFactory.CreatePublishConnection())
            using (var channel = connection.CreateModel())
            {
                var properties = channel.CreateBasicProperties();

                properties.MessageId = message.MessageId;

                channel.BasicPublish(string.Empty, ReceiverQueue, false, properties, message.Body);

                if (await Task.WhenAny(headerCollectionWasNull.Task, Task.Delay(IncomingMessageTimeout)) != headerCollectionWasNull.Task)
                {
                    Assert.Fail("Message receive timed out");
                }

                var headersWasNull = await headerCollectionWasNull.Task;
                Assert.True(headersWasNull, "Header collection is null for classic queues due to a client bug");
            }
        }

        class MyMessage
        {
        }
    }
}