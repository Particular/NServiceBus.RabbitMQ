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
        public async Task Header_collection_is_null_after_redelivery_for_headerless_messages()
        {
            var message = new OutgoingMessage(Guid.NewGuid().ToString(), [], Array.Empty<byte>());
            var headerCollectionWasNullOnFirstDelivery = false;
            var headerCollectionWasNullOnRedelivery = new TaskCompletionSource<bool>();

            OnMessage = (mc, __) =>
            {
                var basicDeliverEventArgs = mc.Extensions.Get<BasicDeliverEventArgs>();

                if (!basicDeliverEventArgs.Redelivered)
                {
                    headerCollectionWasNullOnFirstDelivery = basicDeliverEventArgs.BasicProperties.Headers == null;
                    throw new Exception("Some failure");
                }

                headerCollectionWasNullOnRedelivery.SetResult(basicDeliverEventArgs.BasicProperties.Headers == null);

                return Task.CompletedTask;
            };

            OnError = (ec, __) => Task.FromResult(ErrorHandleResult.RetryRequired);

            using (var connection = connectionFactory.CreatePublishConnection())
            using (var channel = connection.CreateModel())
            {
                var properties = channel.CreateBasicProperties();

                properties.MessageId = message.MessageId;

                channel.BasicPublish(string.Empty, ReceiverQueue, false, properties, message.Body);

                if (await Task.WhenAny(headerCollectionWasNullOnRedelivery.Task, Task.Delay(IncomingMessageTimeout)) != headerCollectionWasNullOnRedelivery.Task)
                {
                    Assert.Fail("Message receive timed out");
                }

                var headersWasNullOnRedelivery = await headerCollectionWasNullOnRedelivery.Task;

                Assert.That(headerCollectionWasNullOnFirstDelivery, Is.True, "Header collection should be null on the first delivery");
                Assert.That(headersWasNullOnRedelivery, Is.True, "Header collection should be null even after a redelivery");
            }
        }

        class MyMessage
        {
        }
    }
}