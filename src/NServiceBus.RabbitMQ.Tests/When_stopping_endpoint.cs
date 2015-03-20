namespace NServiceBus.Transports.RabbitMQ.Tests
{
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Unicast;

    [TestFixture]
    class When_stopping_endpoint : RabbitMqContext
    {
        [SetUp]
        public new void SetUp()
        {
            MakeSureQueueAndExchangeExists(ReceiverQueue);
        }

        [Test, Explicit]
        public void Should__gracefully_shutdown()
        {
            dequeueStrategy.Stop();

            var address = Address.Parse(ReceiverQueue);

            Parallel.For(0, 2000, i =>
                sender.Send(new TransportMessage(){Body = new byte[1]}, new SendOptions(address)));

            dequeueStrategy.Start(50);
            Thread.Sleep(1000);
            dequeueStrategy.Stop();
            connectionManager.Dispose();
        }
    }
}