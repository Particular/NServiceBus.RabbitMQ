namespace NServiceBus.Transports.RabbitMQ.Tests
{
    using System;
    using System.Collections.Generic;
    using NServiceBus.Extensibility;
    using NServiceBus.Routing;
    using NServiceBus.Support;
    using NUnit.Framework;

    using Headers = NServiceBus.Headers;

    [TestFixture]
    class When_consuming_messages : RabbitMqContext
    {
        [SetUp]
        public new void SetUp()
        {
            MakeSureQueueAndExchangeExists(ReceiverQueue);
        }

        [Test]
        public void Should_block_until_a_message_is_available()
        {
            var message = new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), new byte[0]);

            var transportOperations = new TransportOperations(new TransportOperation(message, new UnicastAddressTag(ReceiverQueue)));

            messageSender.Dispatch(transportOperations, new ContextBag());

            var received = WaitForMessage();


            Assert.AreEqual(message.MessageId, received.MessageId);
        }


        [Test]
        public void Should_be_able_to_receive_messages_without_headers()
        {
            var message = new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), new byte[0]);

            using (var channel = connectionManager.GetPublishConnection().CreateModel())
            {
                var properties = channel.CreateBasicProperties();

                properties.MessageId = message.MessageId;

                channel.BasicPublish(string.Empty, ReceiverQueue, true, properties, message.Body);
            }

            var received = WaitForMessage();


            Assert.AreEqual(message.MessageId, received.MessageId);
        }

        [Test]
        public void Should_be_able_to_receive_a_blank_message()
        {
            var message = new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), new byte[0]);


            using (var channel = connectionManager.GetPublishConnection().CreateModel())
            {
                var properties = channel.CreateBasicProperties();

                properties.MessageId = message.MessageId;

                channel.BasicPublish(string.Empty, ReceiverQueue, true, properties, message.Body);
            }

            var received = WaitForMessage();


            Assert.NotNull(received.MessageId, "The message id should be defaulted to a new guid if not set");
        }


        [Test]
        public void Should_up_convert_the_native_type_to_the_enclosed_message_types_header_if_empty()
        {
            var message = new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), new byte[0]);

            var typeName = typeof(MyMessage).FullName;

            using (var channel = connectionManager.GetPublishConnection().CreateModel())
            {
                var properties = channel.CreateBasicProperties();

                properties.MessageId = message.MessageId;
                properties.Type = typeName;

                channel.BasicPublish(string.Empty, ReceiverQueue, true, properties, message.Body);
            }

            var received = WaitForMessage();

            Assert.AreEqual(typeName, received.Headers[Headers.EnclosedMessageTypes]);
            Assert.AreEqual(typeof(MyMessage), Type.GetType(received.Headers[Headers.EnclosedMessageTypes]));
        }

        [Test]
        public void Should_listen_to_the_callback_queue_as_well()
        {
            var message = new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), new byte[0]);

            using (var channel = connectionManager.GetPublishConnection().CreateModel())
            {
                var properties = channel.CreateBasicProperties();

                properties.MessageId = message.MessageId;

                channel.BasicPublish(string.Empty, ReceiverQueue + "." + RuntimeEnvironment.MachineName, true, properties, message.Body);
            }


            var received = WaitForMessage();


            Assert.AreEqual(message.MessageId, received.MessageId);
        }

        class MyMessage
        {

        }
    }
}