namespace NServiceBus.Transport.RabbitMQ.TransportTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.TransportTests;
    using NUnit.Framework;

    public class When_changing_concurrency : NServiceBusTransportTest
    {
        [TestCase(TransportTransactionMode.None)]
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
        [TestCase(TransportTransactionMode.TransactionScope)]
        public async Task Should_complete_current_message(TransportTransactionMode transactionMode)
        {
            var triggeredChangeConcurrency = CreateTaskCompletionSource();
            Task concurrencyChanged = null;
            int invocationCounter = 0;

            await StartPump(async (context, ct) =>
                {
                    Interlocked.Increment(ref invocationCounter);

                    concurrencyChanged = Task.Run(async () =>
                    {
                        var task = receiver.ChangeConcurrency(new PushRuntimeSettings(1), ct);
                        triggeredChangeConcurrency.SetResult();
                        await task;
                    }, ct);

                    await triggeredChangeConcurrency.Task;

                }, (_, _) =>
                {
                    Assert.Fail("Message processing should not fail");
                    return Task.FromResult(ErrorHandleResult.RetryRequired);
                },
                transactionMode);

            await SendMessage(InputQueueName);
            await concurrencyChanged;
            await StopPump();
            Assert.AreEqual(1, invocationCounter, "message should successfully complete on first processing attempt");
        }

        [TestCase(TransportTransactionMode.None)]
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
        [TestCase(TransportTransactionMode.TransactionScope)]
        public async Task Should_dispatch_messages_from_on_error(TransportTransactionMode transactionMode)
        {
            int invocationCounter = 0;

            var triggeredChangeConcurrency = CreateTaskCompletionSource();
            var sentMessageReceived = CreateTaskCompletionSource();

            await StartPump((context, _) =>
                {
                    Interlocked.Increment(ref invocationCounter);
                    if (context.Headers.TryGetValue("FromOnError", out var value) && value == bool.TrueString)
                    {
                        sentMessageReceived.SetResult();
                    }

                    throw new Exception("triggering recoverability pipeline");
                }, async (context, ct) =>
                {
                    // same behavior as delayed retries
                    _ = Task.Run(async () =>
                    {
                        var task = receiver.ChangeConcurrency(new PushRuntimeSettings(1), ct);
                        triggeredChangeConcurrency.SetResult();
                        await task;
                    }, ct);

                    await triggeredChangeConcurrency.Task;
                    await SendMessage(InputQueueName,
                        new Dictionary<string, string>() { { "FromOnError", bool.TrueString } },
                        context.TransportTransaction, cancellationToken: ct);
                    return ErrorHandleResult.Handled;
                },
                transactionMode);

            await SendMessage(InputQueueName);

            await sentMessageReceived.Task;
            await StopPump();
            Assert.AreEqual(2, invocationCounter, "there should be exactly 2 messages (initial message and new message from onError pipeline)");
        }
    }
}