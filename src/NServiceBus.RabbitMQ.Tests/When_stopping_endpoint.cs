namespace NServiceBus.Transports.RabbitMQ.Tests
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using NServiceBus.Routing;

    [TestFixture]
    class When_stopping_endpoint : RabbitMqContext
    {
        [SetUp]
        public new void SetUp()
        {
            MakeSureQueueAndExchangeExists(ReceiverQueue);
        }

        [Test, Explicit]
        public async Task Should__gracefully_shutdown()
        {
            await messagePump.Stop();

            //TODO: maybe build up a IEnumerable<TransportOperation> and Dispatch all at once instead?
            var tasks = new List<Task>();

            for (int i = 0; i < 2000; i++)
            {
                //TODO: need real arguments for OutgoingMessage
                var task = messageSender.Dispatch(new[] { new TransportOperation(new OutgoingMessage("", new Dictionary<string, string>(), new byte[1]), new DispatchOptions(new UnicastAddressTag(ReceiverQueue), DispatchConsistency.Default)) }, new Extensibility.ContextBag());
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