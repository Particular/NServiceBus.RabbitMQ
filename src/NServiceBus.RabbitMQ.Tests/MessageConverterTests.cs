namespace NServiceBus.Transports.RabbitMQ.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Text;
    using global::RabbitMQ.Client.Framing;
    using NUnit.Framework;
    using global::RabbitMQ.Client.Events;

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

            var messageId = converter.RetrieveMessageId(message);
            var headers = converter.RetrieveHeaders(message);

            Assert.IsNotNull(messageId);
            Assert.IsNotNull(headers);
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
        public void Should_set_replyto_header_if_present_in_native_message_and_not_already_set()
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
        public void Should_not_override_replyto_header_if_native_replyto_is_present()
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
            Assert.AreEqual("nsb set address", headers[Headers.ReplyToAddress]);
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
