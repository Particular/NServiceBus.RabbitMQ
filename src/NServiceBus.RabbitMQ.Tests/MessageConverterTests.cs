namespace NServiceBus.Transport.RabbitMQ.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Text;
    using global::RabbitMQ.Client.Events;
    using global::RabbitMQ.Client.Framing;
    using NUnit.Framework;

    [TestFixture]
    class MessageConverterTests
    {
        MessageConverter converter = new MessageConverter();

        [Test]
        public void TestCanHandleNoInterestingProperties()
        {
            var message = new BasicDeliverEventArgs
            {
                BasicProperties = new BasicProperties
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
                BasicProperties = new BasicProperties()
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
                BasicProperties = new BasicProperties()
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
                BasicProperties = new BasicProperties()
            };

            var headers = new Dictionary<string, string> { { Headers.MessageId, "Blah" } };

            var messageId = customConverter.RetrieveMessageId(message, headers);

            Assert.AreEqual(messageId, "Blah");
        }

        [Test]
        public void TestCanHandleByteArrayHeader()
        {
            var message = new BasicDeliverEventArgs
            {
                BasicProperties = new BasicProperties
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
                BasicProperties = new BasicProperties
                {
                    ReplyTo = "myaddress",
                    MessageId = "Blah"
                }
            };

            var headers = converter.RetrieveHeaders(message);

            Assert.NotNull(headers);
            Assert.AreEqual("myaddress", headers[Headers.ReplyToAddress]);
        }

        [Test]
        public void Should_override_replyto_header_if_native_replyto_is_present()
        {
            var message = new BasicDeliverEventArgs
            {
                BasicProperties = new BasicProperties
                {
                    ReplyTo = "myaddress",
                    MessageId = "Blah",
                    Headers = new Dictionary<string, object>
                    {
                        {Headers.ReplyToAddress, Encoding.UTF8.GetBytes("nsb set address")}
                    }
                }
            };

            var headers = converter.RetrieveHeaders(message);

            Assert.NotNull(headers);
            Assert.AreEqual("myaddress", headers[Headers.ReplyToAddress]);
        }

        [Test]
        public void TestCanHandleHeadersWithAllAmqpFieldValues()
        {
            var message = new BasicDeliverEventArgs
            {
                BasicProperties = new BasicProperties
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
                BasicProperties = new BasicProperties
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
                BasicProperties = new BasicProperties
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
        public void TestCanHandleStringArrayListsHeader()
        {
            var message = new BasicDeliverEventArgs
            {
                BasicProperties = new BasicProperties
                {
                    MessageId = "Blah",
                    Headers = new Dictionary<string, object>
                {
                    {"Foo", new ArrayList{"Bing"}}
                }
                }
            };

            var headers = converter.RetrieveHeaders(message);

            Assert.NotNull(headers);
            Assert.AreEqual("Bing", headers["Foo"]);
        }

        [Test]
        public void TestCanHandleStringObjectListHeader()
        {
            var message = new BasicDeliverEventArgs
            {
                BasicProperties = new BasicProperties
                {
                    MessageId = "Blah",
                    Headers = new Dictionary<string, object>
                    {
                        {"Foo", new List<object>{"Bing"}}
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
                BasicProperties = new BasicProperties
                {
                    MessageId = "Blah",
                    Headers = new Dictionary<string, object>
                {
                    {"Foo", new List<object>{new Dictionary<string, object>{{"key1", Encoding.UTF8.GetBytes("value1")}, {"key2", Encoding.UTF8.GetBytes("value2")}}}}
                }
                }
            };

            var headers = converter.RetrieveHeaders(message);

            Assert.NotNull(headers);
            Assert.AreEqual("key1=value1,key2=value2", Convert.ToString(headers["Foo"]));
        }
    }
}
