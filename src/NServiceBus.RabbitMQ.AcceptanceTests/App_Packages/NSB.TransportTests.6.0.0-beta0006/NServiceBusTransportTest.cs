﻿namespace NServiceBus.TransportTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Security.Principal;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using NUnit.Framework;
    using Routing;
    using Settings;
    using Transport;

    public abstract class NServiceBusTransportTest
    {
        [SetUp]
        public void SetUp()
        {
            testId = Guid.NewGuid().ToString();
        }

        static IConfigureTransportInfrastructure CreateConfigurer() => new ConfigureTransportInfrastructure();

        [TearDown]
        public void TearDown()
        {
            testCancellationTokenSource?.Dispose();
            MessagePump?.Stop().GetAwaiter().GetResult();
            Configurer?.Cleanup().GetAwaiter().GetResult();

            transportSettings.Clear();
        }

        protected async Task StartPump(Func<MessageContext, Task> onMessage, Func<ErrorContext, Task<ErrorHandleResult>> onError, TransportTransactionMode transactionMode, Action<string, Exception> onCriticalError = null)
        {
            InputQueueName = GetTestName() + transactionMode;
            ErrorQueueName = $"{InputQueueName}.error";

            transportSettings.Set("NServiceBus.Routing.EndpointName", InputQueueName);

            var queueBindings = new QueueBindings();
            queueBindings.BindReceiving(InputQueueName);
            queueBindings.BindSending(ErrorQueueName);
            transportSettings.Set<QueueBindings>(queueBindings);

            Configurer = CreateConfigurer();

            var configuration = Configurer.Configure(transportSettings, transactionMode);

            TransportInfrastructure = configuration.TransportInfrastructure;

            IgnoreUnsupportedTransactionModes(transactionMode);

            ReceiveInfrastructure = TransportInfrastructure.ConfigureReceiveInfrastructure();
            SendInfrastructure = TransportInfrastructure.ConfigureSendInfrastructure();

            lazyDispatcher = new Lazy<IDispatchMessages>(() => SendInfrastructure.DispatcherFactory());

            MessagePump = ReceiveInfrastructure.MessagePumpFactory();

            var queueCreator = ReceiveInfrastructure.QueueCreatorFactory();
            await queueCreator.CreateQueueIfNecessary(queueBindings, WindowsIdentity.GetCurrent().Name);

            var pushSettings = new PushSettings(InputQueueName, ErrorQueueName, configuration.PurgeInputQueueOnStartup, transactionMode);
            await MessagePump.Init(
                context =>
                {
                    if (context.Headers.ContainsKey(TestIdHeaderName) &&
                        context.Headers[TestIdHeaderName] == testId)
                    {
                        return onMessage(context);
                    }

                    return Task.FromResult(0);
                },
                context =>
                {
                    if (context.Message.Headers.ContainsKey(TestIdHeaderName) &&
                        context.Message.Headers[TestIdHeaderName] == testId)
                    {
                        return onError(context);
                    }

                    return Task.FromResult(ErrorHandleResult.Handled);
                },
                new FakeCriticalError(onCriticalError),
                pushSettings);

            MessagePump.Start(PushRuntimeSettings.Default);
        }

        void IgnoreUnsupportedTransactionModes(TransportTransactionMode requestedTransactionMode)
        {
            if (TransportInfrastructure.TransactionMode < requestedTransactionMode)
            {
                Assert.Ignore($"Only relevant for transports supporting {requestedTransactionMode} or higher");
            }
        }

        protected Task SendMessage(string address, Dictionary<string, string> headers = null, TransportTransaction transportTransaction = null)
        {
            var messageId = Guid.NewGuid().ToString();
            var message = new OutgoingMessage(messageId, headers ?? new Dictionary<string, string>(), new byte[0]);

            if (message.Headers.ContainsKey(TestIdHeaderName) == false)
            {
                message.Headers.Add(TestIdHeaderName, testId);
            }

            var dispatcher = lazyDispatcher.Value;

            if (transportTransaction == null)
            {
                transportTransaction = new TransportTransaction();
            }

            return dispatcher.Dispatch(new TransportOperations(new TransportOperation(message, new UnicastAddressTag(address))), transportTransaction, new ContextBag());
        }

        protected void OnTestTimeout(Action onTimeoutAction)
        {
            testCancellationTokenSource = new CancellationTokenSource();

            testCancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(10));
            testCancellationTokenSource.Token.Register(onTimeoutAction);
        }

        protected virtual TransportInfrastructure CreateTransportInfrastructure()
        {
            var msmqTransportDefinition = new MsmqTransport();
            return msmqTransportDefinition.Initialize(new SettingsHolder(), "");
        }

        static string GetTestName()
        {
            var index = 1;
            var frame = new StackFrame(index);
            Type type;

            while (true)
            {
                type = frame.GetMethod().DeclaringType;
                if (type != null && !type.IsAbstract && typeof(NServiceBusTransportTest).IsAssignableFrom(type))
                {
                    break;
                }

                frame = new StackFrame(++index);
            }

            var classCallingUs = type.FullName.Split('.').Last();

            var testName = classCallingUs.Split('+').First();

            testName = testName.Replace("When_", "");

            testName = Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(testName);

            testName = testName.Replace("_", "");

            return testName;
        }

        protected string InputQueueName;
        protected string ErrorQueueName;

        string testId;

        SettingsHolder transportSettings = new SettingsHolder();
        Lazy<IDispatchMessages> lazyDispatcher;
        TransportReceiveInfrastructure ReceiveInfrastructure;
        TransportSendInfrastructure SendInfrastructure;
        TransportInfrastructure TransportInfrastructure;
        IPushMessages MessagePump;
        CancellationTokenSource testCancellationTokenSource;
        IConfigureTransportInfrastructure Configurer;

        static string MsmqDescriptorKey = "MsmqTransport";
        static string TestIdHeaderName = "TransportTest.TestId";

        class FakeCriticalError : CriticalError
        {
            public FakeCriticalError(Action<string, Exception> errorAction) : base(null)
            {
                this.errorAction = errorAction ?? ((s, e) => { });
            }

            public override void Raise(string errorMessage, Exception exception)
            {
                errorAction(errorMessage, exception);
            }

            Action<string, Exception> errorAction;
        }

        class EnvironmentHelper
        {
            public static string GetEnvironmentVariable(string variable)
            {
                var candidate = Environment.GetEnvironmentVariable(variable, EnvironmentVariableTarget.User);

                if (string.IsNullOrWhiteSpace(candidate))
                {
                    return Environment.GetEnvironmentVariable(variable);
                }

                return candidate;
            }
        }
    }
}