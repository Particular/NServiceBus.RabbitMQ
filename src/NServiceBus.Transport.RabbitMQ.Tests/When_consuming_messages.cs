namespace NServiceBus.Transport.RabbitMQ.Tests
{
    using System;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using NUnit.Framework;
    using Routing;

    [TestFixture]
    class When_consuming_messages : RabbitMqContext
    {
        [Test]
        public async Task Should_block_until_a_message_is_available()
        {
            var message = new OutgoingMessage(Guid.NewGuid().ToString(), [], Array.Empty<byte>());
            var transportOperations = new TransportOperations(new TransportOperation(message, new UnicastAddressTag(ReceiverQueue)));

            await messageDispatcher.Dispatch(transportOperations, new TransportTransaction());

            var receivedMessage = ReceiveMessage();

            Assert.That(receivedMessage.MessageId, Is.EqualTo(message.MessageId));
        }

        [Test]
        public async Task Should_be_able_to_receive_messages_without_headers()
        {
            var message = new OutgoingMessage(Guid.NewGuid().ToString(), [], Array.Empty<byte>());

            using (var connection = await connectionFactory.CreatePublishConnection())
            using (var channel = await connection.CreateChannelAsync())
            {
                var properties = new BasicProperties
                {
                    MessageId = message.MessageId
                };

                await channel.BasicPublishAsync(string.Empty, ReceiverQueue, false, properties, message.Body);
            }

            var receivedMessage = ReceiveMessage();

            Assert.That(receivedMessage.MessageId, Is.EqualTo(message.MessageId));
        }

        [Test]
        public async Task Should_move_message_without_message_id_to_error_queue()
        {
            var message = new OutgoingMessage(Guid.NewGuid().ToString(), [], Array.Empty<byte>());

            using (var connection = await connectionFactory.CreatePublishConnection())
            using (var channel = await connection.CreateChannelAsync())
            {
                var properties = new BasicProperties();

                await channel.BasicPublishAsync(string.Empty, ReceiverQueue, false, properties, message.Body);

                var messageWasReceived = TryWaitForMessageReceipt();

                var result = await channel.BasicGetAsync(ErrorQueue, true);

                Assert.Multiple(() =>
                {
                    Assert.That(messageWasReceived, Is.False, "Message should not be processed successfully.");
                    Assert.That(result, Is.Not.Null, "Message should be considered poison and moved to the error queue.");
                });
            }
        }

        [Test]
        public async Task Should_handle_retries_for_messages_without_headers()
        {
            var message = new OutgoingMessage(Guid.NewGuid().ToString(), [], Array.Empty<byte>());
            var numRetries = 0;
            var handled = new TaskCompletionSource<bool>();

            OnMessage = (_, __) => throw new Exception("Simulated exception");

            OnError = (ec, __) =>
            {
                if (numRetries == 0)
                {
                    numRetries++;
                    return Task.FromResult(ErrorHandleResult.RetryRequired);
                }

                handled.SetResult(true);

                return Task.FromResult(ErrorHandleResult.Handled);
            };

            using (var connection = await connectionFactory.CreatePublishConnection())
            using (var channel = await connection.CreateChannelAsync())
            {
                var properties = new BasicProperties
                {
                    MessageId = message.MessageId
                };

                await channel.BasicPublishAsync(string.Empty, ReceiverQueue, false, properties, message.Body);

                if (await Task.WhenAny(handled.Task, Task.Delay(IncomingMessageTimeout)) != handled.Task)
                {
                    Assert.Fail("Message receive timed out");
                }

                var wasHandled = await handled.Task;
                Assert.Multiple(() =>
                {
                    Assert.That(wasHandled, Is.True, "Error handler should be called after retry");
                    Assert.That(numRetries, Is.EqualTo(1), "Message should be retried once");
                });
            }
        }

        [Test]
        public async Task Header_collection_is_not_null_after_redelivery_for_headerless_messages()
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

            using (var connection = await connectionFactory.CreatePublishConnection())
            using (var channel = await connection.CreateChannelAsync())
            {
                var properties = new BasicProperties
                {
                    MessageId = message.MessageId
                };

                await channel.BasicPublishAsync(string.Empty, ReceiverQueue, false, properties, message.Body);

                if (await Task.WhenAny(headerCollectionWasNullOnRedelivery.Task, Task.Delay(IncomingMessageTimeout)) != headerCollectionWasNullOnRedelivery.Task)
                {
                    Assert.Fail("Message receive timed out");
                }

                var headersWasNullOnRedelivery = await headerCollectionWasNullOnRedelivery.Task;

                Assert.Multiple(() =>
                {
                    Assert.That(headerCollectionWasNullOnFirstDelivery, Is.True, "Header collection should be null on the first delivery");
                    Assert.That(headersWasNullOnRedelivery, Is.False, "Header collection should not null after a redelivery since broker headers are added to quorum queue messages");
                });
            }
        }

        [Test]
        public async Task Should_up_convert_the_native_type_to_the_enclosed_message_types_header_if_empty()
        {
            var message = new OutgoingMessage(Guid.NewGuid().ToString(), [], Array.Empty<byte>());

            var typeName = typeof(MyMessage).FullName;

            using (var connection = await connectionFactory.CreatePublishConnection())
            using (var channel = await connection.CreateChannelAsync())
            {
                var properties = new BasicProperties
                {
                    MessageId = message.MessageId,
                    Type = typeName
                };

                await channel.BasicPublishAsync(string.Empty, ReceiverQueue, false, properties, message.Body);
            }

            var receivedMessage = ReceiveMessage();

            Assert.Multiple(() =>
            {
                Assert.That(receivedMessage.Headers[NServiceBus.Headers.EnclosedMessageTypes], Is.EqualTo(typeName));
                Assert.That(Type.GetType(receivedMessage.Headers[NServiceBus.Headers.EnclosedMessageTypes]), Is.EqualTo(typeof(MyMessage)));
            });
        }

        class MyMessage
        {

        }
    }
}