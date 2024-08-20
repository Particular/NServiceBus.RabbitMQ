namespace NServiceBus.Transport.RabbitMQ.TransportTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus.TransportTests;
    using NUnit.Framework;

    public class When_modifying_incoming_headers : NServiceBusTransportTest
    {
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
        [TestCase(TransportTransactionMode.TransactionScope)]
        public async Task Should_roll_back_header_modifications_between_processing_attempts(TransportTransactionMode transactionMode)
        {
            var messageRetries = new TaskCompletionSource<MessageContext>();
            var firstInvocation = true;

            await StartPump(
                (context, _) =>
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
                (_, __) => Task.FromResult(ErrorHandleResult.RetryRequired),
                transactionMode);

            await SendMessage(InputQueueName, new Dictionary<string, string>
            {
                {"test-header", "original"}
            });

            var retriedMessage = await messageRetries.Task;

            Assert.That(retriedMessage.Headers["test-header"], Is.EqualTo("original"));
        }

        [TestCase(TransportTransactionMode.None)]
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
        [TestCase(TransportTransactionMode.TransactionScope)]
        public async Task Should_roll_back_header_modifications_before_handling_error(TransportTransactionMode transactionMode)
        {
            var errorHandled = new TaskCompletionSource<ErrorContext>();

            await StartPump(
                (context, _) =>
                {
                    context.Headers["test-header"] = "modified";
                    throw new Exception();
                },
                (context, _) =>
                {
                    errorHandled.SetResult(context);
                    return Task.FromResult(ErrorHandleResult.Handled);
                },
                transactionMode);

            await SendMessage(InputQueueName, new Dictionary<string, string>
            {
                {"test-header", "original"}
            });

            var errorContext = await errorHandled.Task;

            Assert.That(errorContext.Message.Headers["test-header"], Is.EqualTo("original"));
        }

        [TestCase(TransportTransactionMode.ReceiveOnly)]
        [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
        [TestCase(TransportTransactionMode.TransactionScope)]
        public async Task Should_roll_back_header_modifications_made_while_handling_error(TransportTransactionMode transactionMode)
        {
            var messageRetries = new TaskCompletionSource<MessageContext>();
            var firstInvocation = true;

            await StartPump(
                (context, _) =>
                {
                    if (firstInvocation)
                    {
                        firstInvocation = false;
                        throw new Exception();
                    }

                    messageRetries.SetResult(context);
                    return Task.FromResult(0);
                },
                (context, _) =>
                {
                    context.Message.Headers["test-header"] = "modified";
                    return Task.FromResult(ErrorHandleResult.RetryRequired);
                },
                transactionMode);

            await SendMessage(InputQueueName, new Dictionary<string, string>
            {
                {"test-header", "original"}
            });

            var retriedMessage = await messageRetries.Task;

            Assert.That(retriedMessage.Headers["test-header"], Is.EqualTo("original"));
        }
    }
}