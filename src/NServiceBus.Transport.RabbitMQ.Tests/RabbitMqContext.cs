namespace NServiceBus.Transport.RabbitMQ.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client.Events;
    using NUnit.Framework;

    class RabbitMqContext
    {
        public virtual int MaximumConcurrency => 1;

        [SetUp]
        public async Task SetUp()
        {
            receivedMessages = new BlockingCollection<IncomingMessage>();

            var connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString") ?? "host=localhost";

            var useTls = connectionString.StartsWith("https", StringComparison.InvariantCultureIgnoreCase) || connectionString.StartsWith("amqps", StringComparison.InvariantCultureIgnoreCase);

            var transport = new RabbitMQTransport(RoutingTopology.Conventional(QueueType.Classic), connectionString);
            var connectionConfig = transport.ConnectionConfiguration;

            connectionFactory = new ConnectionFactory(ReceiverQueue, connectionConfig, null, true, false, transport.HeartbeatInterval, transport.NetworkRecoveryInterval, null);

            infra = await transport.Initialize(new HostSettings(ReceiverQueue, ReceiverQueue, new StartupDiagnosticEntries(),
                (_, __, ___) => { }, true), new[]
            {
                new ReceiveSettings(ReceiverQueue, new QueueAddress(ReceiverQueue), true, true, "error")
            }, AdditionalReceiverQueues.Concat(new[] { ErrorQueue }).ToArray());

            messageDispatcher = infra.Dispatcher;
            messagePump = infra.Receivers[ReceiverQueue];
            subscriptionManager = messagePump.Subscriptions;
            OnError = (_) => ErrorHandleResult.Handled;

            await messagePump.Initialize(new PushRuntimeSettings(MaximumConcurrency),
                (messageContext, cancellationToken) =>
                {
                    var deliverArgs = messageContext.Extensions.Get<BasicDeliverEventArgs>();

                    if (deliverArgs.BasicProperties.AppId == "fail")
                    {
                        throw new Exception("Simulated exception");
                    }

                    receivedMessages.Add(new IncomingMessage(messageContext.NativeMessageId, messageContext.Headers,
                        messageContext.Body), cancellationToken);
                    return Task.CompletedTask;
                },
                (errorContext, __) => Task.FromResult(OnError(errorContext))
            );

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

        protected virtual IEnumerable<string> AdditionalReceiverQueues => Enumerable.Empty<string>();

        protected Func<ErrorContext, ErrorHandleResult> OnError;

        protected const string ReceiverQueue = "testreceiver";
        protected const string ErrorQueue = "error";
        protected ConnectionFactory connectionFactory;
        protected IMessageDispatcher messageDispatcher;
        protected IMessageReceiver messagePump;
        protected ISubscriptionManager subscriptionManager;

        BlockingCollection<IncomingMessage> receivedMessages;

        static readonly TimeSpan IncomingMessageTimeout = TimeSpan.FromSeconds(5);
        TransportInfrastructure infra;
    }
}
