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
                (_, _) => Task.CompletedTask,
                (_, _) => Task.FromResult(ErrorHandleResult.Handled),
                transactionMode);

            await receiver.StopReceive();
            Assert.DoesNotThrowAsync(() => receiver.StopReceive());
        }

        [TestCase(TransportTransactionMode.None)]
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
        [TestCase(TransportTransactionMode.TransactionScope)]
        public async Task Should_allow_restarting_receivers(TransportTransactionMode transactionMode)
        {
            var messageReceived = CreateTaskCompletionSource();

            await StartPump((_, _) =>
                {
                    messageReceived.SetResult();
                    return Task.CompletedTask;
                },
                (_, _) => Task.FromResult(ErrorHandleResult.Handled), transactionMode);

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
            var pumpStopTriggered = CreateTaskCompletionSource();
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
                                var t = receiver.StopReceive(token);
                                pumpStopTriggered.SetResult();
                                await t;
                                TestContext.WriteLine("Stopped receiver");
                            }, token);

                            await pumpStopTriggered.Task;
                            // send message after StopReceive has been called
                            await SendMessage(InputQueueName,
                                new Dictionary<string, string>() { { "Type", "Followup" } },
                                context.TransportTransaction, cancellationToken: token);

                            // restart pump after endpoint has been stopped and the message has been sent
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
                (context, _) =>
                {
                    Assert.Fail($"Message failed processing: {context.Exception}");
                    return Task.FromResult(ErrorHandleResult.Handled);
                }, transactionMode);

            await SendMessage(InputQueueName, new Dictionary<string, string>() { { "Type", "Start" } });
            await followupMessageReceived.Task;
            await StopPump();

            Assert.AreEqual(2, receivedMessages.Count);
            Assert.That(receivedMessages.TryDequeue(out var firstMessageType), Is.True);
            Assert.AreEqual("Start", firstMessageType);
            Assert.That(receivedMessages.TryDequeue(out var secondMessageType), Is.True);
            Assert.AreEqual("Followup", secondMessageType);
        }
    }
}