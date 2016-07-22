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
    class HeaderConverterTests
    {
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

            var headers = HeaderConverter.RetrieveHeaders(message);

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

            var headers = HeaderConverter.RetrieveHeaders(message);

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

            var headers = HeaderConverter.RetrieveHeaders(message);

            Assert.NotNull(headers);
            Assert.AreEqual("myaddress", headers[Headers.ReplyToAddress]);
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

            var headers = HeaderConverter.RetrieveHeaders(message);

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

            var headers = HeaderConverter.RetrieveHeaders(message);

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

            var headers = HeaderConverter.RetrieveHeaders(message);

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

            var headers = HeaderConverter.RetrieveHeaders(message);

            Assert.NotNull(headers);
            Assert.AreEqual("key1=value1,key2=value2", Convert.ToString(headers["Foo"]));
        }
    }
}