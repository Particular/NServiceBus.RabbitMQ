namespace NServiceBus.TransportTests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Transport;

    public class When_restarting_endpoint : NServiceBusTransportTest
    {
        [TestCase(TransportTransactionMode.None)]
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
        [TestCase(TransportTransactionMode.TransactionScope)]
        public async Task Should_handle_multiple_stop_calls_to_started_receiver(TransportTransactionMode transactionMode)
        {
            await StartPump(
                (context, token) => Task.CompletedTask,
                (context, token) => Task.FromResult(ErrorHandleResult.Handled),
                transactionMode);

            await receiver.StopReceive();
            await receiver.StopReceive();
        }

        [TestCase(TransportTransactionMode.None)]
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
        [TestCase(TransportTransactionMode.TransactionScope)]
        public async Task Should_allow_restarting_receivers(TransportTransactionMode transactionMode)
        {
            var messageReceived = CreateTaskCompletionSource();

            await StartPump((context, token) =>
            {
                messageReceived.SetResult();
                return Task.CompletedTask;
            },
                (context, token) => Task.FromResult(ErrorHandleResult.Handled), transactionMode);

            await receiver.StopReceive();
            await receiver.StartReceive();

            await SendMessage(InputQueueName);
            await messageReceived.Task;
        }

        [TestCase(TransportTransactionMode.None)]
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
        [TestCase(TransportTransactionMode.TransactionScope)]
        public async Task Should_gracefully_restart_processing(TransportTransactionMode transactionMode)
        {
            var receivedMessages = new ConcurrentQueue<string>();
            var followupMessageReceived = CreateTaskCompletionSource();
            await StartPump(async (context, token) =>
            {
                var messageType = context.Headers["Type"];
                receivedMessages.Enqueue(messageType);
                TestContext.WriteLine("Received message " + messageType);

                switch (messageType)
                {
                    case "Start":

                        // run async because the pump might block the return until all inflight messages are processed.
                        var stopTask = Task.Run(async () =>
                        {
                            await receiver.StopReceive(token);
                            TestContext.WriteLine("Stopped receiver");
                        }, token);

                        await SendMessage(InputQueueName, new Dictionary<string, string>() { { "Type", "Followup" } },
                            context.TransportTransaction, cancellationToken: token);
                        await Task.Yield();

                        _ = stopTask.ContinueWith(async _ =>
                        {
                            await receiver.StartReceive(token);
                            TestContext.WriteLine("Started receiver");
                        }, token);

                        break;
                    case "Followup":
                        followupMessageReceived.SetResult();
                        break;
                    default:
                        throw new ArgumentException();
                }
            },
                (context, token) =>
                {
                    Assert.Fail($"Message failed processing: {context.Exception}");
                    return Task.FromResult(ErrorHandleResult.Handled);
                }, transactionMode);

            await SendMessage(InputQueueName, new Dictionary<string, string>() { { "Type", "Start" } });
            await followupMessageReceived.Task;

            Assert.AreEqual(2, receivedMessages.Count);
            Assert.IsTrue(receivedMessages.TryDequeue(out var firstMessageType));
            Assert.AreEqual("Start", firstMessageType);
            Assert.IsTrue(receivedMessages.TryDequeue(out var secondMessageType));
            Assert.AreEqual("Followup", secondMessageType);
        }
    }
}