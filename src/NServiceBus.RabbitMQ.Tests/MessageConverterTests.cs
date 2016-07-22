namespace NServiceBus.Transport.RabbitMQ.Tests
{
    using global::RabbitMQ.Client.Events;
    using global::RabbitMQ.Client.Framing;
    using NUnit.Framework;

    [TestFixture]
    class MessageConverterTests
    {
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

            var messageId = MessageConverter.DefaultMessageIdStrategy(message);
            var headers = HeaderConverter.RetrieveHeaders(message);

            Assert.IsNotNull(messageId);
            Assert.IsNotNull(headers);
        }
    }
}
