namespace NServiceBus.Transports.RabbitMQ.Tests
{
    using System.Threading.Tasks;
    using NUnit.Framework;

    [TestFixture]
    class When_stopping_endpoint : RabbitMqContext
    {
        [Test, Explicit]
        public async Task Should_gracefully_shutdown()
        {
            await messagePump.Stop();

            var operations = new OutgoingMessageBuilder().WithBody(new byte[1]).SendTo(ReceiverQueue).Build(10000);
            await messageSender.Dispatch(operations, new Extensibility.ContextBag());

            messagePump.Start(new PushRuntimeSettings(50));
            await Task.Delay(500);
            await messagePump.Stop();
        }
    }
}