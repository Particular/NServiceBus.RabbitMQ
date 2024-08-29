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

            DeliveryModes IBasicProperties.DeliveryMode { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            DeliveryModes IReadOnlyBasicProperties.DeliveryMode => throw new NotImplementedException();

            public void ClearAppId()
            {
                throw new NotSupportedException();
            }

            public void ClearClusterId()
            {
                throw new NotSupportedException();
            }

            public void ClearContentEncoding()
            {
                throw new NotSupportedException();
            }

            public void ClearContentType()
            {
                throw new NotSupportedException();
            }

            public void ClearCorrelationId()
            {
                throw new NotSupportedException();
            }

            public void ClearDeliveryMode()
            {
                throw new NotSupportedException();
            }

            public void ClearExpiration()
            {
                throw new NotSupportedException();
            }

            public void ClearHeaders()
            {
                throw new NotSupportedException();
            }

            public void ClearMessageId()
            {
                throw new NotSupportedException();
            }

            public void ClearPriority()
            {
                throw new NotSupportedException();
            }

            public void ClearReplyTo()
            {
                throw new NotSupportedException();
            }

            public void ClearTimestamp()
            {
                throw new NotSupportedException();
            }

            public void ClearType()
            {
                throw new NotSupportedException();
            }

            public void ClearUserId()
            {
                throw new NotSupportedException();
            }

            public bool IsAppIdPresent()
            {
                throw new NotSupportedException();
            }

            public bool IsClusterIdPresent()
            {
                throw new NotSupportedException();
            }

            public bool IsContentEncodingPresent()
            {
                throw new NotSupportedException();
            }

            public bool IsContentTypePresent()
            {
                throw new NotSupportedException();
            }

            public bool IsCorrelationIdPresent() => !string.IsNullOrEmpty(CorrelationId);

            public bool IsDeliveryModePresent() => DeliveryMode != 0;

            public bool IsExpirationPresent()
            {
                throw new NotSupportedException();
            }

            public bool IsHeadersPresent()
            {
                throw new NotSupportedException();
            }

            public bool IsMessageIdPresent() => !string.IsNullOrEmpty(MessageId);

            public bool IsPriorityPresent()
            {
                throw new NotSupportedException();
            }

            public bool IsReplyToPresent() => !string.IsNullOrEmpty(ReplyTo);

            public bool IsTimestampPresent()
            {
                throw new NotSupportedException();
            }

            public bool IsTypePresent() => !string.IsNullOrEmpty(Type);

            public bool IsUserIdPresent()
            {
                throw new NotSupportedException();
            }
        }

        MessageConverter converter = new MessageConverter(MessageConverter.DefaultMessageIdStrategy);

        [Test]
        public void TestCanHandleNoInterestingProperties()
        {
            var basicProperties = new TestingBasicProperties
            {
                MessageId = "Blah"
            };

            var message = new BasicDeliverEventArgs(default, default, default, default, default, basicProperties, default);

            var headers = converter.RetrieveHeaders(message);
            var messageId = converter.RetrieveMessageId(message, headers);

            Assert.Multiple(() =>
            {
                Assert.That(messageId, Is.Not.Null);
                Assert.That(headers, Is.Not.Null);
            });
        }

        [Test]
        public void Should_throw_exception_when_no_message_id_is_set()
        {
            var basicProperties = new TestingBasicProperties();
            var message = new BasicDeliverEventArgs(default, default, default, default, default, basicProperties, default);

            var headers = new Dictionary<string, string>();

            Assert.Throws<InvalidOperationException>(() => converter.RetrieveMessageId(message, headers));
        }

        [Test]
        public void Should_throw_exception_when_using_custom_strategy_and_no_message_id_is_returned()
        {
            var customConverter = new MessageConverter(args => "");

            var basicProperties = new TestingBasicProperties();
            var message = new BasicDeliverEventArgs(default, default, default, default, default, basicProperties, default);

            var headers = new Dictionary<string, string>();

            Assert.Throws<InvalidOperationException>(() => customConverter.RetrieveMessageId(message, headers));
        }

        [Test]
        public void Should_fall_back_to_message_id_header_when_custom_strategy_returns_empty_string()
        {
            var customConverter = new MessageConverter(args => "");

            var basicProperties = new TestingBasicProperties();
            var message = new BasicDeliverEventArgs(default, default, default, default, default, basicProperties, default);

            var headers = new Dictionary<string, string> { { NServiceBus.Headers.MessageId, "Blah" } };

            var messageId = customConverter.RetrieveMessageId(message, headers);

            Assert.That(messageId, Is.EqualTo("Blah"));
        }

        [Test]
        public void TestCanHandleByteArrayHeader()
        {
            var basicProperties = new TestingBasicProperties
            {
                MessageId = "Blah",
                Headers = new Dictionary<string, object>
                {
                    {"Foo", Encoding.UTF8.GetBytes("blah")}
                }
            };

            var message = new BasicDeliverEventArgs(default, default, default, default, default, basicProperties, default);

            var headers = converter.RetrieveHeaders(message);

            Assert.That(headers, Is.Not.Null);
            Assert.That(headers["Foo"], Is.EqualTo("blah"));
        }

        [Test]
        public void Should_set_replyto_header_if_native_replyto_is_present()
        {
            var basicProperties = new TestingBasicProperties
            {
                ReplyTo = "myaddress",
                MessageId = "Blah"
            };

            var message = new BasicDeliverEventArgs(default, default, default, default, default, basicProperties, default);

            var headers = converter.RetrieveHeaders(message);

            Assert.That(headers, Is.Not.Null);
            Assert.That(headers[NServiceBus.Headers.ReplyToAddress], Is.EqualTo("myaddress"));
        }

        [Test]
        public void Should_override_replyto_header_if_native_replyto_is_present()
        {
            var basicProperties = new TestingBasicProperties
            {
                ReplyTo = "myaddress",
                MessageId = "Blah",
                Headers = new Dictionary<string, object>
                {
                    {NServiceBus.Headers.ReplyToAddress, Encoding.UTF8.GetBytes("nsb set address")}
                }
            };

            var message = new BasicDeliverEventArgs(default, default, default, default, default, basicProperties, default);

            var headers = converter.RetrieveHeaders(message);

            Assert.That(headers, Is.Not.Null);
            Assert.That(headers[NServiceBus.Headers.ReplyToAddress], Is.EqualTo("myaddress"));
        }

        [Test]
        public void TestCanHandleHeadersWithAllAmqpFieldValues()
        {
            var basicProperties = new TestingBasicProperties
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
            };

            var message = new BasicDeliverEventArgs(default, default, default, default, default, basicProperties, default);

            var headers = converter.RetrieveHeaders(message);

            Assert.That(headers, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(headers["short"], Is.EqualTo("42"));
                Assert.That(headers["int"], Is.EqualTo("42"));
                Assert.That(headers["long"], Is.EqualTo("42"));
                Assert.That(headers["decimal"], Is.EqualTo("42"));
                Assert.That(headers["sbyte"], Is.EqualTo("42"));
                Assert.That(headers["double"], Is.EqualTo("42"));
                Assert.That(headers["single"], Is.EqualTo("42"));
                Assert.That(headers["bool"], Is.EqualTo("True"));
            });
        }

        [Test]
        public void TestCanHandleAmqpTimestampHeader()
        {
            var basicProperties = new TestingBasicProperties
            {
                MessageId = "Blah",
                Headers = new Dictionary<string, object>
                {
                    {"Foo", new AmqpTimestamp(int.MaxValue) }
                }
            };

            var message = new BasicDeliverEventArgs(default, default, default, default, default, basicProperties, default);

            var headers = converter.RetrieveHeaders(message);

            Assert.That(headers, Is.Not.Null);
            Assert.That(headers["Foo"], Is.EqualTo("2038-01-19 03:14:07:000000 Z"));
        }

        [Test]
        public void TestCanHandleStringHeader()
        {
            var basicProperties = new TestingBasicProperties
            {
                MessageId = "Blah",
                Headers = new Dictionary<string, object>
                {
                    {"Foo", "ni"}
                }
            };

            var message = new BasicDeliverEventArgs(default, default, default, default, default, basicProperties, default);

            var headers = converter.RetrieveHeaders(message);

            Assert.That(headers, Is.Not.Null);
            Assert.That(headers["Foo"], Is.EqualTo("ni"));
        }

        [Test]
        public void TestCanHandleStringObjectListHeader()
        {
            var basicProperties = new TestingBasicProperties
            {
                MessageId = "Blah",
                Headers = new Dictionary<string, object>
                {
                    {"Foo", new List<object> {"Bing"}}
                }
            };

            var message = new BasicDeliverEventArgs(default, default, default, default, default, basicProperties, default);

            var headers = converter.RetrieveHeaders(message);

            Assert.That(headers, Is.Not.Null);
            Assert.That(headers["Foo"], Is.EqualTo("Bing"));
        }

        [Test]
        public void TestCanHandleTablesListHeader()
        {
            var basicProperties = new TestingBasicProperties
            {
                MessageId = "Blah",
                Headers = new Dictionary<string, object>
                {
                    {"Foo", new List<object> {new Dictionary<string, object> {{"key1", Encoding.UTF8.GetBytes("value1")}, {"key2", Encoding.UTF8.GetBytes("value2")}}}}
                }
            };

            var message = new BasicDeliverEventArgs(default, default, default, default, default, basicProperties, default);

            var headers = converter.RetrieveHeaders(message);

            Assert.Multiple(() =>
            {
                Assert.That(headers, Is.Not.Null);
                Assert.That(Convert.ToString(headers["Foo"]), Is.EqualTo("key1=value1,key2=value2"));
            });
        }
    }
}
