namespace NServiceBus.Transport.RabbitMQ.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;

    abstract class RabbitMqContext
    {
        public virtual int MaximumConcurrency => 1;

        [SetUp]
        public virtual async Task SetUp()
        {
            receivedMessages = [];

            var connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString") ?? "host=localhost";

            var useTls = connectionString.StartsWith("https", StringComparison.InvariantCultureIgnoreCase) || connectionString.StartsWith("amqps", StringComparison.InvariantCultureIgnoreCase);

            var transport = new RabbitMQTransport(RoutingTopology.Conventional(queueType), connectionString)
            {
                // The startup costs for creating a policy for every test queue add up, and the tests shouldn't be impacted by the default delivery limit.
                ValidateDeliveryLimits = false
            };

            var connectionConfig = transport.ConnectionConfiguration;

            connectionFactory = new ConnectionFactory(ReceiverQueue, connectionConfig, null, true, false, transport.HeartbeatInterval, transport.NetworkRecoveryInterval, null, null);

            infra = await transport.Initialize(new HostSettings(ReceiverQueue, ReceiverQueue, new StartupDiagnosticEntries(), (_, _, _) => { }, true),
                [new ReceiveSettings(ReceiverQueue, new QueueAddress(ReceiverQueue), true, true, ErrorQueue)], [.. AdditionalReceiverQueues, ErrorQueue]);

            messageDispatcher = infra.Dispatcher;
            messagePump = infra.Receivers[ReceiverQueue];
            subscriptionManager = messagePump.Subscriptions;
            OnMessage = (messageContext, cancellationToken) =>
            {
                receivedMessages.Add(new IncomingMessage(messageContext.NativeMessageId, messageContext.Headers, messageContext.Body), cancellationToken);
                return Task.CompletedTask;
            };

            OnError = (_, _) => Task.FromResult(ErrorHandleResult.Handled);

            await messagePump.Initialize(
                new PushRuntimeSettings(MaximumConcurrency),
                (messageContext, cancellationToken) => OnMessage(messageContext, cancellationToken),
                (errorContext, cancellationToken) => OnError(errorContext, cancellationToken));

            await messagePump.StartReceive();
        }

        [TearDown]
        public async Task TearDown()
        {
            if (messagePump != null)
            {
                await messagePump.StopReceive();
            }

            if (infra != null)
            {
                await infra.Shutdown();
            }
        }

        protected bool TryWaitForMessageReceipt() => TryReceiveMessage(out var _, IncomingMessageTimeout);

        protected IncomingMessage ReceiveMessage()
        {
            if (!TryReceiveMessage(out var message, IncomingMessageTimeout))
            {
                throw new TimeoutException($"The message did not arrive within {IncomingMessageTimeout.TotalSeconds} seconds.");
            }

            return message;
        }

        bool TryReceiveMessage(out IncomingMessage message, TimeSpan timeout) =>
            receivedMessages.TryTake(out message, timeout);

        protected IList<string> AdditionalReceiverQueues = [];

        protected Func<MessageContext, CancellationToken, Task> OnMessage;
        protected Func<ErrorContext, CancellationToken, Task<ErrorHandleResult>> OnError;

        protected string ReceiverQueue => GetTestQueueName("testreceiver");
        protected string ErrorQueue => GetTestQueueName("error");

        protected string GetTestQueueName(string queueName) => $"{queueName}-{queueType}";

        protected QueueType queueType = QueueType.Quorum;
        protected ConnectionFactory connectionFactory;
        protected IMessageDispatcher messageDispatcher;
        protected IMessageReceiver messagePump;
        protected ISubscriptionManager subscriptionManager;

        BlockingCollection<IncomingMessage> receivedMessages;

        protected static readonly TimeSpan IncomingMessageTimeout = TimeSpan.FromSeconds(5);
        TransportInfrastructure infra;
    }
}
