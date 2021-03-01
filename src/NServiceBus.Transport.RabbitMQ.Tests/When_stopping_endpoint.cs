namespace NServiceBus.Transport.RabbitMQ.Tests
{
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;

    [TestFixture]
    class When_stopping_endpoint : RabbitMqContext
    {
        [Test]
        [Explicit]
        public async Task Should_gracefully_shutdown()
        {
            await messagePump.StopReceive(CancellationToken.None);

            var operations = new OutgoingMessageBuilder().WithBody(new byte[1]).SendTo(ReceiverQueue).Build(10000);
            await messageDispatcher.Dispatch(operations, new TransportTransaction(), CancellationToken.None);

            await messagePump.StartReceive(CancellationToken.None);
            await Task.Delay(500);
            await messagePump.StopReceive(CancellationToken.None);
        }
    }
}