namespace NServiceBus.Transport.RabbitMQ.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Routing;

    [TestFixture]
    class When_consuming_messages : RabbitMqContext
    {
        [Test]
        public async Task Should_block_until_a_message_is_available()
        {
            var message = new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), new byte[0]);
            var transportOperations = new TransportOperations(new TransportOperation(message, new UnicastAddressTag(ReceiverQueue)));

            await messageDispatcher.Dispatch(transportOperations, new TransportTransaction());

            var receivedMessage = ReceiveMessage();

            Assert.AreEqual(message.MessageId, receivedMessage.MessageId);
        }

        [Test]
        public void Should_be_able_to_receive_messages_without_headers()
        {
            var message = new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), new byte[0]);

            using (var connection = connectionFactory.CreatePublishConnection())
            using (var channel = connection.CreateModel())
            {
                var properties = channel.CreateBasicProperties();

                properties.MessageId = message.MessageId;

                channel.BasicPublish(string.Empty, ReceiverQueue, false, properties, message.Body);
            }

            var receivedMessage = ReceiveMessage();

            Assert.AreEqual(message.MessageId, receivedMessage.MessageId);
        }

        [Test]
        public void Should_move_message_without_message_id_to_error_queue()
        {
            var message = new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), new byte[0]);

            using (var connection = connectionFactory.CreatePublishConnection())
            using (var channel = connection.CreateModel())
            {
                var properties = channel.CreateBasicProperties();

                channel.BasicPublish(string.Empty, ReceiverQueue, false, properties, message.Body);

                var messageWasReceived = TryWaitForMessageReceipt();

                var result = channel.BasicGet(ErrorQueue, true);

                Assert.False(messageWasReceived, "Message should not be processed successfully.");
                Assert.NotNull(result, "Message should be considered poison and moved to the error queue.");
            }
        }

        [Test]
        public void Should_handle_retries_for_messages_without_headers()
        {
            var message = new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), new byte[0]);
            var numRetries = 0;

            CustomErrorHandling[message.MessageId] = (ec) =>
            {
                if (numRetries == 0)
                {
                    numRetries++;
                    return ErrorHandleResult.RetryRequired;
                }

                return ErrorHandleResult.Handled;
            };

            using (var connection = connectionFactory.CreatePublishConnection())
            using (var channel = connection.CreateModel())
            {
                var properties = channel.CreateBasicProperties();

                properties.AppId = "fail";
                properties.MessageId = message.MessageId;

                channel.BasicPublish(string.Empty, ReceiverQueue, false, properties, message.Body);

                var messageWasReceived = TryWaitForMessageReceipt();

                var result = channel.BasicGet(ErrorQueue, true);

                Assert.False(messageWasReceived, "Message should not be processed successfully.");
                Assert.AreEqual(1, numRetries, "Message should be retried once");
                Assert.NotNull(result, "Message should be considered poison and moved to the error queue.");
            }
        }

        [Test]
        public void Should_up_convert_the_native_type_to_the_enclosed_message_types_header_if_empty()
        {
            var message = new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), new byte[0]);

            var typeName = typeof(MyMessage).FullName;

            using (var connection = connectionFactory.CreatePublishConnection())
            using (var channel = connection.CreateModel())
            {
                var properties = channel.CreateBasicProperties();

                properties.MessageId = message.MessageId;
                properties.Type = typeName;

                channel.BasicPublish(string.Empty, ReceiverQueue, false, properties, message.Body);
            }

            var receivedMessage = ReceiveMessage();

            Assert.AreEqual(typeName, receivedMessage.Headers[Headers.EnclosedMessageTypes]);
            Assert.AreEqual(typeof(MyMessage), Type.GetType(receivedMessage.Headers[Headers.EnclosedMessageTypes]));
        }

        class MyMessage
        {

        }
    }
}