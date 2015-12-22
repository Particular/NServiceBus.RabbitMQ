namespace NServiceBus.Transports.RabbitMQ.Tests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;

    [TestFixture]
    class When_stopping_endpoint : RabbitMqContext
    {
        [SetUp]
        public new void SetUp()
        {
            MakeSureQueueAndExchangeExists(ReceiverQueue);
        }

        [Test, Explicit]
        public async Task Should_gracefully_shutdown()
        {
            await messagePump.Stop();

            //TODO: maybe build up a IEnumerable<TransportOperation> and Dispatch all at once instead?
            var tasks = new List<Task>();

            for (var i = 0; i < 2000; i++)
            {
                var operations = new OutgoingMessageBuilder().WithBody(new byte[1]).SendTo(ReceiverQueue).Build();
                var task = messageSender.Dispatch(operations, new Extensibility.ContextBag());

                tasks.Add(task);
            }

            await Task.WhenAll(tasks);

            messagePump.Start(new PushRuntimeSettings(50));
            await Task.Delay(1000);
            await messagePump.Stop();
            connectionManager.Dispose();
        }
    }
}