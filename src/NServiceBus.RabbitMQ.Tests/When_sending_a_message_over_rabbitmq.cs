namespace NServiceBus.Transports.RabbitMQ.Tests
{
    using System;
    using System.IO;
    using System.Text;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using NServiceBus.Extensibility;
    using NUnit.Framework;
    using Unicast.Queuing;

    [TestFixture]
    class When_sending_a_message_over_rabbitmq : RabbitMqContext
    {
        [Test]
        public void Should_populate_the_body()
        {
            var body = Encoding.UTF8.GetBytes("<TestMessage/>");

            Verify(new TransportMessageBuilder().WithBody(body),
                 received => Assert.AreEqual(body, received.Body));
        }


        [Test]
        public void Should_set_the_content_type()
        {
            VerifyRabbit(new TransportMessageBuilder().WithHeader(Headers.ContentType, "application/json"),
                received => Assert.AreEqual("application/json", received.BasicProperties.ContentType));

        }

        
        [Test]
        public void Should_default_the_content_type_to_octet_stream_when_no_content_type_is_specified()
        {
            VerifyRabbit(new TransportMessageBuilder(),
                received => Assert.AreEqual("application/octet-stream", received.BasicProperties.ContentType));

        }

        

        [Test]
        public void Should_set_the_message_type_based_on_the_encoded_message_types_header()
        {
            var messageType = typeof (MyMessage);

            VerifyRabbit(new TransportMessageBuilder().WithHeader(Headers.EnclosedMessageTypes, messageType.AssemblyQualifiedName),
                received => Assert.AreEqual(messageType.FullName, received.BasicProperties.Type));

        }

        [Test]
        public void Should_set_the_time_to_be_received()
        {

            var timeToBeReceived = TimeSpan.FromDays(1);


            VerifyRabbit(new TransportMessageBuilder().TimeToBeReceived(timeToBeReceived),
                received => Assert.AreEqual(timeToBeReceived.TotalMilliseconds.ToString(), received.BasicProperties.Expiration));
        }

        [Test]
        public void Should_set_the_reply_to_address()
        {
            var address = "myAddress";

            Verify(new TransportMessageBuilder().ReplyToAddress(address), 
                (t, r) =>
                {
                    Assert.AreEqual(address, t.Headers[Headers.ReplyToAddress]);
                    Assert.AreEqual(address, r.BasicProperties.ReplyTo);
                });

        }

        [Test]
        public void Should_not_populate_the_callback_header()
        {
            Assert.Fail("CallbackHeaderKey no longer exists. OK to delete test?");
            //Verify(new TransportMessageBuilder(),
            //    (t, r) => Assert.IsFalse(t.Headers.ContainsKey(RabbitMqMessageSender.CallbackHeaderKey)));

        }
       
        [Test]
        public void Should_set_correlation_id_if_present()
        {
            var correlationId = Guid.NewGuid().ToString();

            Verify(new TransportMessageBuilder().CorrelationId(correlationId),
                result => Assert.AreEqual(correlationId, result.Headers[Headers.CorrelationId]));

        }

        [Test]
        public void Should_preserve_the_recoverable_setting_if_set_to_durable()
        {
            Assert.Fail("Recoverable is no longer a flag on incoming message. OK to delete test?");
            //Verify(new TransportMessageBuilder(),result => Assert.True(result.Recoverable));
        }


        [Test]
        public void Should_preserve_the_recoverable_setting_if_set_to_non_durable()
        {
            Assert.Fail("Recoverable is no longer a flag on incoming message. OK to delete test?");
            //Verify(new TransportMessageBuilder().NonDurable(), result => Assert.False(result.Recoverable));
        }


        [Test]
        public void Should_transmit_all_transportMessage_headers()
        {

            Verify(new TransportMessageBuilder().WithHeader("h1", "v1").WithHeader("h2", "v2"),
                result =>
                {
                    Assert.AreEqual("v1", result.Headers["h1"]);
                    Assert.AreEqual("v2", result.Headers["h2"]);
                });

        }

        [Test, Ignore("Not sure we should enforce this")]
        public void Should_throw_when_sending_to_a_non_existing_queue()
        {
            Assert.Throws<QueueNotFoundException>(() =>
                messageSender.Dispatch(new[]
                    {
                        new TransportMessageBuilder().SendTo("NonExistingQueue@localhost").Build()
                    }, 
                    new ContextBag()
                )
            );
        }

        void Verify(TransportMessageBuilder builder, Action<IncomingMessage, BasicDeliverEventArgs> assertion,string alternateQueueToReceiveOn=null)
        {
            var operation = builder.SendTo("testEndPoint").Build();

            MakeSureQueueAndExchangeExists("testEndPoint");

            SendMessage(operation);

            var result = Consume(operation.Message.MessageId, alternateQueueToReceiveOn);

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
        void Verify(TransportMessageBuilder builder, Action<IncomingMessage> assertion)
        {
            Verify(builder, (t, r) => assertion(t));
        }

        void VerifyRabbit(TransportMessageBuilder builder, Action<BasicDeliverEventArgs> assertion, string alternateQueueToReceiveOn = null)
        {
            Verify(builder, (t, r) => assertion(r), alternateQueueToReceiveOn);
        }

        void SendMessage(TransportOperation operation)
        {
            messageSender.Dispatch(new[] { operation }, new ContextBag());
        }

        BasicDeliverEventArgs Consume(string id, string queueToReceiveOn)
        {
            if (string.IsNullOrEmpty(queueToReceiveOn))
            {
                queueToReceiveOn = "testEndPoint";
            }

            using (var channel = connectionManager.GetConsumeConnection().CreateModel())
            {
                var consumer = new QueueingBasicConsumer(channel);

                channel.BasicConsume(queueToReceiveOn, false, consumer);

                BasicDeliverEventArgs message;

                if (!consumer.Queue.Dequeue(1000, out message))
                    throw new InvalidOperationException("No message found in queue");

                var e = message;

                if (e.BasicProperties.MessageId != id)
                    throw new InvalidOperationException("Unexpected message found in queue");

                channel.BasicAck(e.DeliveryTag,false);

                return e;
            }
        }



        class MyMessage
        {
            
        }

    }
}