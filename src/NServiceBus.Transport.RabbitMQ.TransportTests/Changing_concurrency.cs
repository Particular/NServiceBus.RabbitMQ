namespace NServiceBus.Transport.RabbitMQ.TransportTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.TransportTests;
    using NUnit.Framework;

    public class Changing_concurrency : NServiceBusTransportTest
    {
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        public async Task ImmediateRetries(TransportTransactionMode transactionMode)
        {
            var messageRetries = new TaskCompletionSource<MessageContext>();
            int invocationCounter = 0;

            await StartPump(async (context, ct) =>
                {
                    Interlocked.Increment(ref invocationCounter);

                    await receiver.ChangeConcurrency(new PushRuntimeSettings(1), ct);
                    await Task.Delay(500, ct);

                    messageRetries.SetResult(context);
                }, (_, ct) =>
                {
                    Assert.Fail();
                    return Task.FromResult(ErrorHandleResult.RetryRequired);
                },
                transactionMode);

            await SendMessage(InputQueueName);
            await messageRetries.Task;
            await Task.Delay(1000); // take some time to process any remaining message
            Assert.AreEqual(1, invocationCounter);
        }

        [TestCase(TransportTransactionMode.ReceiveOnly)]
        public async Task DelayedRetries(TransportTransactionMode transactionMode)
        {
            var firstInvocation = true;
            int invocationCounter = 0;

            var errorPipelineMessageReceived = CreateTaskCompletionSource();

            await StartPump((context, _) =>
                {
                    Interlocked.Increment(ref invocationCounter);
                    if (context.Headers.TryGetValue("FromOnError", out var value) && value == bool.TrueString)
                    {
                        errorPipelineMessageReceived.SetResult();
                    }
                    else if (firstInvocation)
                    {
                        firstInvocation = false;
                        throw new Exception("some exception");
                    }
                    else
                    {
                        Assert.Fail("input message should be completed after onError");
                    }

                    return Task.CompletedTask;
                }, async (context, ct) =>
                {
                    TestContext.WriteLine(context.Exception);

                    // same behavior as delayed retries
                    await receiver.ChangeConcurrency(new PushRuntimeSettings(1), ct);
                    await Task.Yield();
                    await SendMessage(InputQueueName,
                        new Dictionary<string, string>() { { "FromOnError", bool.TrueString } },
                        context.TransportTransaction, cancellationToken: ct);
                    return ErrorHandleResult.Handled;
                },
                transactionMode);

            await SendMessage(InputQueueName);

            await errorPipelineMessageReceived.Task;
            await Task.Delay(1000); // take some time to process any remaining message
            Assert.AreEqual(2, invocationCounter);
        }
    }
}