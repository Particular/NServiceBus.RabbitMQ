namespace NServiceBus.Transport.RabbitMQ.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using NUnit.Framework;

    [TestFixture]
    class MessageConverterTests
    {
        class TestingBasicProperties : IBasicProperties
        {
            public string AppId { get; set; }
            public string ClusterId { get; set; }
            public string ContentEncoding { get; set; }
            public string ContentType { get; set; }
            public string CorrelationId { get; set; }
            public byte DeliveryMode { get; set; }
            public string Expiration { get; set; }
            public IDictionary<string, object> Headers { get; set; }
            public string MessageId { get; set; }
            public bool Persistent { get; set; }
            public byte Priority { get; set; }
            public string ReplyTo { get; set; }
            public PublicationAddress ReplyToAddress { get; set; }
            public AmqpTimestamp Timestamp { get; set; }
            public string Type { get; set; }
            public string UserId { get; set; }

            public ushort ProtocolClassId => throw new NotSupportedException();

            public string ProtocolClassName => throw new NotSupportedException();

            public void ClearAppId() => throw new NotSupportedException();

            public void ClearClusterId() => throw new NotSupportedException();

            public void ClearContentEncoding() => throw new NotSupportedException();

            public void ClearContentType() => throw new NotSupportedException();

            public void ClearCorrelationId() => throw new NotSupportedException();

            public void ClearDeliveryMode() => throw new NotSupportedException();

            public void ClearExpiration() => throw new NotSupportedException();

            public void ClearHeaders() => throw new NotSupportedException();

            public void ClearMessageId() => throw new NotSupportedException();

            public void ClearPriority() => throw new NotSupportedException();

            public void ClearReplyTo() => throw new NotSupportedException();

            public void ClearTimestamp() => throw new NotSupportedException();

            public void ClearType() => throw new NotSupportedException();

            public void ClearUserId() => throw new NotSupportedException();

            public bool IsAppIdPresent() => throw new NotSupportedException();

            public bool IsClusterIdPresent() => throw new NotSupportedException();

            public bool IsContentEncodingPresent() => throw new NotSupportedException();

            public bool IsContentTypePresent() => ContentType != null;

            public bool IsCorrelationIdPresent() => !string.IsNullOrEmpty(CorrelationId);

            public bool IsDeliveryModePresent() => DeliveryMode != 0;

            public bool IsExpirationPresent() => throw new NotSupportedException();

            public bool IsHeadersPresent() => throw new NotSupportedException();

            public bool IsMessageIdPresent() => !string.IsNullOrEmpty(MessageId);

            public bool IsPriorityPresent() => throw new NotSupportedException();

            public bool IsReplyToPresent() => !string.IsNullOrEmpty(ReplyTo);

            public bool IsTimestampPresent() => throw new NotSupportedException();

            public bool IsTypePresent() => !string.IsNullOrEmpty(Type);

            public bool IsUserIdPresent() => throw new NotSupportedException();
        }

        MessageConverter converter = new MessageConverter(MessageConverter.DefaultMessageIdStrategy);

        [Test]
        public void TestCanHandleNoInterestingProperties()
        {
            var message = new BasicDeliverEventArgs
            {
                BasicProperties = new TestingBasicProperties
                {
                    MessageId = "Blah"
                }
            };

            var headers = converter.RetrieveHeaders(message);
            var messageId = converter.RetrieveMessageId(message, headers);

            Assert.IsNotNull(messageId);
            Assert.IsNotNull(headers);
        }

        [Test]
        public void Should_throw_exception_when_no_message_id_is_set()
        {
            var message = new BasicDeliverEventArgs
            {
                BasicProperties = new TestingBasicProperties()
            };

            var headers = new Dictionary<string, string>();

            Assert.Throws<InvalidOperationException>(() => converter.RetrieveMessageId(message, headers));
        }

        [Test]
        public void Should_throw_exception_when_using_custom_strategy_and_no_message_id_is_returned()
        {
            var customConverter = new MessageConverter(args => "");

            var message = new BasicDeliverEventArgs
            {
                BasicProperties = new TestingBasicProperties()
            };

            var headers = new Dictionary<string, string>();

            Assert.Throws<InvalidOperationException>(() => customConverter.RetrieveMessageId(message, headers));
        }

        [Test]
        public void Should_fall_back_to_message_id_header_when_custom_strategy_returns_empty_string()
        {
            var customConverter = new MessageConverter(args => "");

            var message = new BasicDeliverEventArgs
            {
                BasicProperties = new TestingBasicProperties()
            };

            var headers = new Dictionary<string, string> { { NServiceBus.Headers.MessageId, "Blah" } };

            var messageId = customConverter.RetrieveMessageId(message, headers);

            Assert.AreEqual(messageId, "Blah");
        }

        [Test]
        public void TestCanHandleByteArrayHeader()
        {
            var message = new BasicDeliverEventArgs
            {
                BasicProperties = new TestingBasicProperties
                {
                    MessageId = "Blah",
                    Headers = new Dictionary<string, object>
                    {
                        {"Foo", Encoding.UTF8.GetBytes("blah")}
                    }
                }
            };

            var headers = converter.RetrieveHeaders(message);

            Assert.NotNull(headers);
            Assert.AreEqual("blah", headers["Foo"]);
        }

        [Test]
        public void Should_set_replyto_header_if_native_replyto_is_present()
        {
            var message = new BasicDeliverEventArgs
            {
                BasicProperties = new TestingBasicProperties
                {
                    ReplyTo = "myaddress",
                    MessageId = "Blah"
                }
            };

            var headers = converter.RetrieveHeaders(message);

            Assert.NotNull(headers);
            Assert.AreEqual("myaddress", headers[NServiceBus.Headers.ReplyToAddress]);
        }

        [Test]
        public void Should_override_replyto_header_if_native_replyto_is_present()
        {
            var message = new BasicDeliverEventArgs
            {
                BasicProperties = new TestingBasicProperties
                {
                    ReplyTo = "myaddress",
                    MessageId = "Blah",
                    Headers = new Dictionary<string, object>
                    {
                        {NServiceBus.Headers.ReplyToAddress, Encoding.UTF8.GetBytes("nsb set address")}
                    }
                }
            };

            var headers = converter.RetrieveHeaders(message);

            Assert.NotNull(headers);
            Assert.AreEqual("myaddress", headers[NServiceBus.Headers.ReplyToAddress]);
        }

        [Test]
        public void TestCanHandleHeadersWithAllAmqpFieldValues()
        {
            var message = new BasicDeliverEventArgs
            {
                BasicProperties = new TestingBasicProperties
                {
                    MessageId = "Blah",
                    Headers = new Dictionary<string, object>
                    {
                        {"short", (short)42},
                        {"int", 42},
                        {"long", 42L},
                        {"decimal", 42m},
                        {"sbyte", (sbyte)42},
                        {"double", 42d},
                        {"single", 42f},
                        {"bool", true }
                    }
                }
            };

            var headers = converter.RetrieveHeaders(message);

            Assert.NotNull(headers);
            Assert.AreEqual("42", headers["short"]);
            Assert.AreEqual("42", headers["int"]);
            Assert.AreEqual("42", headers["long"]);
            Assert.AreEqual("42", headers["decimal"]);
            Assert.AreEqual("42", headers["sbyte"]);
            Assert.AreEqual("42", headers["double"]);
            Assert.AreEqual("42", headers["single"]);
            Assert.AreEqual("True", headers["bool"]);
        }

        [Test]
        public void TestCanHandleAmqpTimestampHeader()
        {
            var message = new BasicDeliverEventArgs
            {
                BasicProperties = new TestingBasicProperties
                {
                    MessageId = "Blah",
                    Headers = new Dictionary<string, object>
                    {
                        {"Foo", new global::RabbitMQ.Client.AmqpTimestamp(int.MaxValue) }
                    }
                }
            };

            var headers = converter.RetrieveHeaders(message);

            Assert.NotNull(headers);
            Assert.AreEqual("2038-01-19 03:14:07:000000 Z", headers["Foo"]);
        }

        [Test]
        public void TestCanHandleStringHeader()
        {
            var message = new BasicDeliverEventArgs
            {
                BasicProperties = new TestingBasicProperties
                {
                    MessageId = "Blah",
                    Headers = new Dictionary<string, object>
                    {
                        {"Foo", "ni"}
                    }
                }
            };

            var headers = converter.RetrieveHeaders(message);

            Assert.NotNull(headers);
            Assert.AreEqual("ni", headers["Foo"]);
        }

        [Test]
        public void TestCanHandleStringObjectListHeader()
        {
            var message = new BasicDeliverEventArgs
            {
                BasicProperties = new TestingBasicProperties
                {
                    MessageId = "Blah",
                    Headers = new Dictionary<string, object>
                    {
                        {"Foo", new List<object> {"Bing"}}
                    }
                }
            };

            var headers = converter.RetrieveHeaders(message);

            Assert.NotNull(headers);
            Assert.AreEqual("Bing", headers["Foo"]);
        }

        [Test]
        public void TestCanHandleTablesListHeader()
        {
            var message = new BasicDeliverEventArgs
            {
                BasicProperties = new TestingBasicProperties
                {
                    MessageId = "Blah",
                    Headers = new Dictionary<string, object>
                {
                    {"Foo", new List<object> {new Dictionary<string, object> {{"key1", Encoding.UTF8.GetBytes("value1")}, {"key2", Encoding.UTF8.GetBytes("value2")}}}}
                }
                }
            };

            var headers = converter.RetrieveHeaders(message);

            Assert.NotNull(headers);
            Assert.AreEqual("key1=value1,key2=value2", Convert.ToString(headers["Foo"]));
        }

        [Test]
        public void Should_handle_content_type()
        {
            var basicProperties = new TestingBasicProperties
            {
                MessageId = "Blah",
                ContentType = "content_type"
            };

            var message = new BasicDeliverEventArgs
            {
                BasicProperties = new TestingBasicProperties
                {
                    MessageId = "Blah",
                    ContentType = "content_type"
                }
            };

            var headers = converter.RetrieveHeaders(message);
            var messageId = converter.RetrieveMessageId(message, headers);

            Assert.Multiple(() =>
            {
                Assert.That(messageId, Is.Not.Null);
                Assert.That(headers, Is.Not.Null);
                Assert.That(headers[NServiceBus.Headers.ContentType], Is.EqualTo(basicProperties.ContentType));
            });
        }
    }
}
