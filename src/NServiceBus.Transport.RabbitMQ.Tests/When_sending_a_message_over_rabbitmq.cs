namespace NServiceBus.Transport.RabbitMQ.Tests
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client.Events;
    using NUnit.Framework;

    [TestFixture]
    class When_sending_a_message_over_rabbitmq : RabbitMqContext
    {
        string QueueToReceiveOn;

        [SetUp]
        public override Task SetUp()
        {
            QueueToReceiveOn = GetTestQueueName("testendpoint");
            AdditionalReceiverQueues.Add(QueueToReceiveOn);

            return base.SetUp();
        }

        [Test]
        public Task Should_populate_the_body()
        {
            var body = Encoding.UTF8.GetBytes("<TestMessage/>");

            return Verify(new OutgoingMessageBuilder().WithBody(body), (IncomingMessage received) => Assert.That(received.Body.Span.SequenceEqual(body), Is.True));
        }

        [Test]
        public Task Should_set_the_content_type()
        {
            return Verify(new OutgoingMessageBuilder().WithHeader(Headers.ContentType, "application/json"), received => Assert.That(received.BasicProperties.ContentType, Is.EqualTo("application/json")));
        }

        [Test]
        public Task Should_default_the_content_type_to_octet_stream_when_no_content_type_is_specified()
        {
            return Verify(new OutgoingMessageBuilder(), received => Assert.That(received.BasicProperties.ContentType, Is.EqualTo("application/octet-stream")));
        }

        [Test]
        public Task Should_set_the_message_type_based_on_the_encoded_message_types_header()
        {
            var messageType = typeof(MyMessage);

            return Verify(new OutgoingMessageBuilder().WithHeader(Headers.EnclosedMessageTypes, messageType.AssemblyQualifiedName), received => Assert.That(received.BasicProperties.Type, Is.EqualTo(messageType.FullName)));
        }

        [Test]
        public Task Should_set_the_time_to_be_received()
        {
            var timeToBeReceived = TimeSpan.FromDays(1);

            return Verify(new OutgoingMessageBuilder().TimeToBeReceived(timeToBeReceived), received => Assert.That(received.BasicProperties.Expiration, Is.EqualTo(timeToBeReceived.TotalMilliseconds.ToString())));
        }

        [Test]
        public Task Should_set_the_reply_to_address()
        {
            var address = "myAddress";

            return Verify(new OutgoingMessageBuilder().ReplyToAddress(address),
                (t, r) =>
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(t.Headers[Headers.ReplyToAddress], Is.EqualTo(address));
                        Assert.That(r.BasicProperties.ReplyTo, Is.EqualTo(address));
                    });
                });
        }

        [Test]
        public Task Should_set_correlation_id_if_present()
        {
            var correlationId = Guid.NewGuid().ToString();

            return Verify(new OutgoingMessageBuilder().CorrelationId(correlationId), result => Assert.That(result.Headers[Headers.CorrelationId], Is.EqualTo(correlationId)));
        }

        [Test]
        public Task Should_honor_the_non_persistent_flag()
        {
            return Verify(new OutgoingMessageBuilder().WithHeader(BasicPropertiesExtensions.UseNonPersistentDeliveryHeader, true.ToString()), (message, basicDeliverEventArgs) =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(basicDeliverEventArgs.BasicProperties.Persistent, Is.False);
                    Assert.That(basicDeliverEventArgs.BasicProperties.Headers.ContainsKey(BasicPropertiesExtensions.UseNonPersistentDeliveryHeader), Is.False, "Temp header should not be visible on the wire");
                    Assert.That(message.Headers.ContainsKey(BasicPropertiesExtensions.UseNonPersistentDeliveryHeader), Is.True, "Temp header should not removed to make sure that retries keeps the setting");
                });
            });
        }

        [Test]
        public Task Should_transmit_all_transportMessage_headers()
        {
            return Verify(new OutgoingMessageBuilder().WithHeader("h1", "v1").WithHeader("h2", "v2"),
                result =>
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(result.Headers["h1"], Is.EqualTo("v1"));
                        Assert.That(result.Headers["h2"], Is.EqualTo("v2"));
                    });
                });
        }

        async Task Verify(OutgoingMessageBuilder builder, Action<IncomingMessage, BasicDeliverEventArgs> assertion, CancellationToken cancellationToken = default)
        {
            var operations = builder.SendTo(QueueToReceiveOn).Build();

            await messageDispatcher.Dispatch(operations, new TransportTransaction(), cancellationToken);

            var messageId = operations.MulticastTransportOperations.FirstOrDefault()?.Message.MessageId ?? operations.UnicastTransportOperations.FirstOrDefault()?.Message.MessageId;

            var result = await Consume(messageId, QueueToReceiveOn, cancellationToken);

            var converter = new MessageConverter(MessageConverter.DefaultMessageIdStrategy);
            var convertedHeaders = converter.RetrieveHeaders(result);
            var convertedMessageId = converter.RetrieveMessageId(result, convertedHeaders);

            var incomingMessage = new IncomingMessage(convertedMessageId, convertedHeaders, result.Body.ToArray());

            assertion(incomingMessage, result);
        }

        Task Verify(OutgoingMessageBuilder builder, Action<IncomingMessage> assertion, CancellationToken cancellationToken = default) => Verify(builder, (t, r) => assertion(t), cancellationToken);

        Task Verify(OutgoingMessageBuilder builder, Action<BasicDeliverEventArgs> assertion, CancellationToken cancellationToken = default) => Verify(builder, (t, r) => assertion(r), cancellationToken);

        async Task<BasicDeliverEventArgs> Consume(string id, string queueToReceiveOn, CancellationToken cancellationToken)
        {
            using (var connection = await connectionFactory.CreateConnection("Consume", cancellationToken: cancellationToken))
            using (var channel = await connection.CreateChannelAsync(cancellationToken))
            {
                var message = await channel.BasicGetAsync(queueToReceiveOn, false, cancellationToken) ?? throw new InvalidOperationException("No message found in queue");

                if (message.BasicProperties.MessageId != id)
                {
                    throw new InvalidOperationException("Unexpected message found in queue");
                }

                await channel.BasicAckAsync(message.DeliveryTag, false, cancellationToken);

                return new BasicDeliverEventArgs("", message.DeliveryTag, message.Redelivered, message.Exchange, message.RoutingKey, message.BasicProperties, message.Body);
            }
        }

        class MyMessage
        {

        }
    }
}
