﻿namespace NServiceBus.Transport.RabbitMQ.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client.Events;
    using Extensibility;
    using Transports;
    using NUnit.Framework;

    using Headers = Headers;

    [TestFixture]
    class When_sending_a_message_over_rabbitmq : RabbitMqContext
    {
        [Test]
        public Task Should_populate_the_body()
        {
            var body = Encoding.UTF8.GetBytes("<TestMessage/>");

            return Verify(new OutgoingMessageBuilder().WithBody(body), (IncomingMessage received) => Assert.AreEqual(body, received.Body));
        }

        [Test]
        public Task Should_set_the_content_type()
        {
            return Verify(new OutgoingMessageBuilder().WithHeader(Headers.ContentType, "application/json"), received => Assert.AreEqual("application/json", received.BasicProperties.ContentType));
        }

        [Test]
        public Task Should_default_the_content_type_to_octet_stream_when_no_content_type_is_specified()
        {
            return Verify(new OutgoingMessageBuilder(), received => Assert.AreEqual("application/octet-stream", received.BasicProperties.ContentType));
        }

        [Test]
        public Task Should_set_the_message_type_based_on_the_encoded_message_types_header()
        {
            var messageType = typeof(MyMessage);

            return Verify(new OutgoingMessageBuilder().WithHeader(Headers.EnclosedMessageTypes, messageType.AssemblyQualifiedName), received => Assert.AreEqual(messageType.FullName, received.BasicProperties.Type));
        }

        [Test]
        public Task Should_set_the_time_to_be_received()
        {
            var timeToBeReceived = TimeSpan.FromDays(1);

            return Verify(new OutgoingMessageBuilder().TimeToBeReceived(timeToBeReceived), received => Assert.AreEqual(timeToBeReceived.TotalMilliseconds.ToString(), received.BasicProperties.Expiration));
        }

        [Test]
        public Task Should_set_the_reply_to_address()
        {
            var address = "myAddress";

            return Verify(new OutgoingMessageBuilder().ReplyToAddress(address),
                (t, r) =>
                {
                    Assert.AreEqual(address, t.Headers[Headers.ReplyToAddress]);
                    Assert.AreEqual(address, r.BasicProperties.ReplyTo);
                });
        }

        [Test]
        public Task Should_set_correlation_id_if_present()
        {
            var correlationId = Guid.NewGuid().ToString();

            return Verify(new OutgoingMessageBuilder().CorrelationId(correlationId), result => Assert.AreEqual(correlationId, result.Headers[Headers.CorrelationId]));
        }

        [Test]
        public Task Should_preserve_the_recoverable_setting_if_set_to_durable()
        {
            return Verify(new OutgoingMessageBuilder(), result => Assert.True(result.Headers[Headers.NonDurableMessage] == "False"));
        }

        [Test]
        public Task Should_preserve_the_recoverable_setting_if_set_to_non_durable()
        {
            return Verify(new OutgoingMessageBuilder().NonDurable(), result => Assert.True(result.Headers[Headers.NonDurableMessage] == "True"));
        }

        [Test]
        public Task Should_transmit_all_transportMessage_headers()
        {
            return Verify(new OutgoingMessageBuilder().WithHeader("h1", "v1").WithHeader("h2", "v2"),
                result =>
                {
                    Assert.AreEqual("v1", result.Headers["h1"]);
                    Assert.AreEqual("v2", result.Headers["h2"]);
                });
        }

        async Task Verify(OutgoingMessageBuilder builder, Action<IncomingMessage, BasicDeliverEventArgs> assertion, string queueToReceiveOn = "testEndPoint")
        {
            var operations = builder.SendTo(queueToReceiveOn).Build();

            MakeSureQueueAndExchangeExists(queueToReceiveOn);

            await messageDispatcher.Dispatch(operations, new ContextBag());

            var messageId = operations.MulticastTransportOperations.FirstOrDefault()?.Message.MessageId ?? operations.UnicastTransportOperations.FirstOrDefault()?.Message.MessageId;

            var result = Consume(messageId, queueToReceiveOn);

            var converter = new MessageConverter();

            using (var body = new MemoryStream(result.Body))
            {
                var incomingMessage = new IncomingMessage(
                    converter.RetrieveMessageId(result),
                    converter.RetrieveHeaders(result),
                    body
                );

                assertion(incomingMessage, result);
            }
        }

        Task Verify(OutgoingMessageBuilder builder, Action<IncomingMessage> assertion, string queueToReceiveOn = "testEndPoint") => Verify(builder, (t, r) => assertion(t), queueToReceiveOn);

        Task Verify(OutgoingMessageBuilder builder, Action<BasicDeliverEventArgs> assertion, string queueToReceiveOn = "testEndPoint") => Verify(builder, (t, r) => assertion(r), queueToReceiveOn);

        BasicDeliverEventArgs Consume(string id, string queueToReceiveOn)
        {
            using (var connection = connectionFactory.CreateConnection("Consume"))
            using (var channel = connection.CreateModel())
            {
                var consumer = new EventingBasicConsumer(channel);

                BasicDeliverEventArgs message = null;
                var resetEvent = new ManualResetEventSlim(false);
                consumer.Received += (sender, args) =>
                {
                    message = args;
                    resetEvent.Set();
                };
                channel.BasicConsume(queueToReceiveOn, false, consumer);
                if (!resetEvent.Wait(1000))
                {
                    throw new InvalidOperationException("No message found in queue");
                }
                if (message.BasicProperties.MessageId != id)
                {
                    throw new InvalidOperationException("Unexpected message found in queue");
                }

                channel.BasicAck(message.DeliveryTag, false);

                return message;
            }
        }

        class MyMessage
        {

        }
    }
}