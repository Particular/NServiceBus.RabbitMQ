namespace NServiceBus.Transport.RabbitMQ.Tests
{
    using System.Threading.Tasks;
    using NServiceBus.Transports;
    using NUnit.Framework;

    [TestFixture]
    class When_stopping_endpoint : RabbitMqContext
    {
        [Test, Explicit]
        public async Task Should_gracefully_shutdown()
        {
            await messagePump.Stop();

            var operations = new OutgoingMessageBuilder().WithBody(new byte[1]).SendTo(ReceiverQueue).Build(10000);
            await messageDispatcher.Dispatch(operations, new Extensibility.ContextBag());

            messagePump.Start(new PushRuntimeSettings(50));
            await Task.Delay(500);
            await messagePump.Stop();
        }
    }
}