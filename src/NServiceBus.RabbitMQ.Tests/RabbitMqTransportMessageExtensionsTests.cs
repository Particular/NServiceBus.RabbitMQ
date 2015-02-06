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
    class RabbitMqTransportMessageExtensionsTests
    {
        MessageConverter converter = new MessageConverter();
        [Test]
        public void TestCanHandleNoInterestingProperties()
        {
            Assert.IsNotNull(converter.ToTransportMessage(new BasicDeliverEventArgs { BasicProperties = new BasicProperties { MessageId = "Blah" } }));
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
            var transportMessage = converter.ToTransportMessage(basicDeliverEventArgs);
            Assert.NotNull(transportMessage);
            Assert.AreEqual("blah", transportMessage.Headers["Foo"]);
        }

        [Test]
        public void Should_set_replyto_header_if_present_in_native_message_and_not_already_set()
        {
            var basicDeliverEventArgs = new BasicDeliverEventArgs
            {
                BasicProperties = new BasicProperties
                {
                    ReplyTo = "myaddress",
                    MessageId = "Blah",
                }
            };
            var transportMessage = converter.ToTransportMessage(basicDeliverEventArgs);
            Assert.NotNull(transportMessage);
            Assert.AreEqual("myaddress", transportMessage.Headers[Headers.ReplyToAddress]);
        }

        [Test]
        public void Should_not_override_replyto_header_if_native_replyto_is_present()
        {
            var basicDeliverEventArgs = new BasicDeliverEventArgs
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
            var transportMessage = converter.ToTransportMessage(basicDeliverEventArgs);
            Assert.NotNull(transportMessage);
            Assert.AreEqual("nsb set address", transportMessage.Headers[Headers.ReplyToAddress]);
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
            var transportMessage = converter.ToTransportMessage(basicDeliverEventArgs);
            Assert.NotNull(transportMessage);
            Assert.AreEqual("ni", transportMessage.Headers["Foo"]);
        }

        [Test]
        public void TestCanHandleStringArrayListsHeader()
        {
            var transportMessage = converter.ToTransportMessage(new BasicDeliverEventArgs
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
            var transportMessage = converter.ToTransportMessage(basicDeliverEventArgs);
            Assert.NotNull(transportMessage);
            Assert.AreEqual("Bing", transportMessage.Headers["Foo"]);
        }
        [Test]
        public void TestCanHandleTablesListHeader()
        {
            var transportMessage = converter.ToTransportMessage(new BasicDeliverEventArgs
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
