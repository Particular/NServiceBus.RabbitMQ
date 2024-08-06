namespace NServiceBus.Transport.RabbitMQ.TransportTests
{
    using System;
    using System.Threading.Tasks;
    using DelayedDelivery;
    using NServiceBus.TransportTests;
    using NUnit.Framework;

    public class When_delayed_delivery_is_disabled : NServiceBusTransportTest
    {
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        public async Task Should_throw_when_sending(TransportTransactionMode transactionMode)
        {
            await StartPump(
                (context, _) => Task.FromResult(0),
                (_, __) => Task.FromResult(ErrorHandleResult.RetryRequired),
                transactionMode);

            Assert.ThrowsAsync<Exception>(async () =>
            {
                var dispatchProperties = new DispatchProperties { DelayDeliveryWith = new DelayDeliveryWith(TimeSpan.FromHours(1)) };
                await SendMessage(InputQueueName, [], null, dispatchProperties);
            });
        }
    }
}