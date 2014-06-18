namespace NServiceBus.Transports.RabbitMQ.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Text;
    using NUnit.Framework;
    using global::RabbitMQ.Client.Events;
    using global::RabbitMQ.Client.Framing.v0_9_1;

    [TestFixture]
    class RabbitMqTransportMessageExtensionsTests
    {
        [Test]
        public void TestCanHandleNoInterestingProperties()
        {
            Assert.IsNotNull(RabbitMqTransportMessageExtensions.ToTransportMessage(new BasicDeliverEventArgs{BasicProperties = new BasicProperties{MessageId = "Blah"}}));
        }

        [Test]
        public void TestCanHandleByteArrayHeader()
        {
            var basicDeliverEventArgs = new BasicDeliverEventArgs
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
            var transportMessage = RabbitMqTransportMessageExtensions.ToTransportMessage(basicDeliverEventArgs);
            Assert.NotNull(transportMessage);
            Assert.AreEqual("blah", transportMessage.Headers["Foo"]);
        }

        [Test]
        public void Should_set_or_override_replyto_header_if_present_in_native_message()
        {
            var basicDeliverEventArgs = new BasicDeliverEventArgs
            {
                BasicProperties = new BasicProperties
                {
                    ReplyTo = "myaddress",
                    MessageId = "Blah",
                    Headers = new Dictionary<string, object>
                    {
                        {Headers.ReplyToAddress, Encoding.UTF8.GetBytes("native address")}
                    }
                }
            };
            var transportMessage = RabbitMqTransportMessageExtensions.ToTransportMessage(basicDeliverEventArgs);
            Assert.NotNull(transportMessage);
            Assert.AreEqual("myaddress", transportMessage.Headers[Headers.ReplyToAddress]);
        }



        [Test]
        public void TestCanHandleStringHeader()
        {
            var basicDeliverEventArgs = new BasicDeliverEventArgs
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
            var transportMessage = RabbitMqTransportMessageExtensions.ToTransportMessage(basicDeliverEventArgs);
            Assert.NotNull(transportMessage);
            Assert.AreEqual("ni", transportMessage.Headers["Foo"]);
        }

        [Test]
        public void TestCanHandleStringArrayListsHeader()
        {
            var transportMessage = RabbitMqTransportMessageExtensions.ToTransportMessage(new BasicDeliverEventArgs
            {
                BasicProperties = new BasicProperties
                {
                    MessageId = "Blah",
                    Headers = new Dictionary<string,object>
                {
                    {"Foo", new ArrayList{"Bing"}}
                }
                }
            });
            Assert.NotNull(transportMessage);
            Assert.AreEqual("Bing", transportMessage.Headers["Foo"]);
        }

        [Test]
        public void TestCanHandleStringObjectListHeader()
        {
            var basicDeliverEventArgs = new BasicDeliverEventArgs
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
            var transportMessage = RabbitMqTransportMessageExtensions.ToTransportMessage(basicDeliverEventArgs);
            Assert.NotNull(transportMessage);
            Assert.AreEqual("Bing", transportMessage.Headers["Foo"]);
        }
        [Test]
        public void TestCanHandleTablesListHeader()
        {
            var transportMessage = RabbitMqTransportMessageExtensions.ToTransportMessage(new BasicDeliverEventArgs
            {
                BasicProperties = new BasicProperties
                {
                    MessageId = "Blah",
                    Headers = new Dictionary<string, object>
                {
                    {"Foo", new List<object>{new Dictionary<string, object>{{"key1", Encoding.UTF8.GetBytes("value1")}, {"key2", Encoding.UTF8.GetBytes("value2")}}}}
                }
                }
            });
            Assert.NotNull(transportMessage);
            Assert.AreEqual("key1=value1,key2=value2", Convert.ToString(transportMessage.Headers["Foo"]));
        }
    }
}
