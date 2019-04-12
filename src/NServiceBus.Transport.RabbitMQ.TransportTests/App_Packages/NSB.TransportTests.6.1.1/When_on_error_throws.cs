namespace NServiceBus.TransportTests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using NUnit.Framework;
    using Transport;

    public class When_on_error_throws : NServiceBusTransportTest
    {
        // [TestCase(TransportTransactionMode.None)] -- not relevant
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
        [TestCase(TransportTransactionMode.TransactionScope)]
        public async Task Should_reinvoke_on_error_with_original_exception(TransportTransactionMode transactionMode)
        {
            var loggerFactory = new TransportTestLoggerFactory();
            LogManager.UseFactory(loggerFactory);

            var onErrorCalled = new TaskCompletionSource<ErrorContext>();
            var criticalErrorCalled = false;
            string criticalErrorMessage = null;

            OnTestTimeout(() => onErrorCalled.SetCanceled());

            var firstInvocation = true;
            string nativeMessageId = null;

            await StartPump(
                context =>
                {
                    nativeMessageId = context.MessageId;

                    throw new Exception("Simulated exception");
                },
                context =>
                {
                    if (firstInvocation)
                    {
                        firstInvocation = false;

                        throw new Exception("Exception from onError");
                    }

                    onErrorCalled.SetResult(context);

                    return Task.FromResult(ErrorHandleResult.Handled);
                },
                transactionMode,
                (message, exception) =>
                {
                    criticalErrorCalled = true;
                    criticalErrorMessage = message;
                });

            await SendMessage(InputQueueName);

            var errorContext = await onErrorCalled.Task;

            Assert.AreEqual("Simulated exception", errorContext.Exception.Message, "Should retry the message");
            Assert.True(criticalErrorCalled, "Should invoke critical error");
            Assert.AreEqual($"Failed to execute recoverability policy for message with native ID: `{nativeMessageId}`", criticalErrorMessage, "Incorrect critical error message.");
            Assert.False(loggerFactory.LogItems.Any(item => item.Level > LogLevel.Info));
        }
    }
}