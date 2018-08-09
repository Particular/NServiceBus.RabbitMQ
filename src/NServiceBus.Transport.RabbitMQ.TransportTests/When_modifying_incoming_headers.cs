namespace NServiceBus.Transport.RabbitMQ.TransportTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus.TransportTests;
    using NUnit.Framework;

    public class When_modifying_incoming_headers : NServiceBusTransportTest
    {
        [Test]
        public async Task Should_roll_back_header_modifications_for_immediate_retries()
        {
            var messageRetries = new TaskCompletionSource<MessageContext>();
            var firstInvocation = true;

            await StartPump(context =>
                {
                    if (firstInvocation)
                    {
                        context.Headers["test-header"] = "modified";
                        firstInvocation = false;
                        throw new Exception();
                    }

                    messageRetries.SetResult(context);
                    return Task.FromResult(0);
                },
                context => Task.FromResult(ErrorHandleResult.RetryRequired),
                TransportTransactionMode.None);

            await SendMessage(InputQueueName, new Dictionary<string, string>
            {
                {"test-header", "original"}
            });

            var retriedMessage = await messageRetries.Task;

            Assert.AreEqual("original", retriedMessage.Headers["test-header"]);
        }

        [Test]
        public async Task Should_roll_back_header_modifications_when_handling_error()
        {
            var errorHandled = new TaskCompletionSource<ErrorContext>();

            await StartPump(context =>
                {
                    context.Headers["test-header"] = "modified";
                    throw new Exception();
                },
                context =>
                {
                    errorHandled.SetResult(context);
                    return Task.FromResult(ErrorHandleResult.Handled);
                },
                TransportTransactionMode.None);

            await SendMessage(InputQueueName, new Dictionary<string, string>
            {
                {"test-header", "original"}
            });

            var errorContext = await errorHandled.Task;

            Assert.AreEqual("original", errorContext.Message.Headers["test-header"]);
        }
    }
}