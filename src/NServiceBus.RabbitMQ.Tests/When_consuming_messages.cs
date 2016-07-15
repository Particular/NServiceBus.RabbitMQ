namespace NServiceBus.Transport.RabbitMQ.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Extensibility;
    using NUnit.Framework;
    using Routing;
    using Transports;

    [TestFixture]
    class When_consuming_messages : RabbitMqContext
    {
        [Test]
        public async Task Should_block_until_a_message_is_available()
        {
            var message = new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), new byte[0]);
            var transportOperations = new TransportOperations(new TransportOperation(message, new UnicastAddressTag(ReceiverQueue)));

            await messageDispatcher.Dispatch(transportOperations, new ContextBag());

            var received = WaitForMessage();

            Assert.AreEqual(message.MessageId, received.MessageId);
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

            var received = WaitForMessage();

            Assert.AreEqual(message.MessageId, received.MessageId);
        }

        [Test]
        public void Should_be_able_to_receive_a_blank_message()
        {
            var message = new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), new byte[0]);

            using (var connection = connectionFactory.CreatePublishConnection())
            using (var channel = connection.CreateModel())
            {
                var properties = channel.CreateBasicProperties();

                properties.MessageId = message.MessageId;

                channel.BasicPublish(string.Empty, ReceiverQueue, false, properties, message.Body);
            }

            var received = WaitForMessage();

            Assert.NotNull(received.MessageId, "The message id should be defaulted to a new guid if not set");
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

            var received = WaitForMessage();

            Assert.AreEqual(typeName, received.Headers[Headers.EnclosedMessageTypes]);
            Assert.AreEqual(typeof(MyMessage), Type.GetType(received.Headers[Headers.EnclosedMessageTypes]));
        }

        class MyMessage
        {

        }
    }
}