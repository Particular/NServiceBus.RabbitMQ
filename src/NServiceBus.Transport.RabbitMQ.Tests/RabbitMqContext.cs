namespace NServiceBus.Transport.RabbitMQ.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;

    class RabbitMqContext
    {
        public virtual int MaximumConcurrency => 1;

        [SetUp]
        public async Task SetUp()
        {
            receivedMessages = new BlockingCollection<IncomingMessage>();

            var connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString");

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new Exception("The 'RabbitMQTransport_ConnectionString' environment variable is not set.");
            }

            var transport = new RabbitMQTransport(Topology.Conventional, connectionString);

            connectionFactory = new ConnectionFactory(ReceiverQueue, transport.Host, transport.Port ?? 5672,
                transport.VHost, transport.UserName, transport.Password, false, null, false,
                false, transport.HeartbeatInterval, transport.NetworkRecoveryInterval);

            infra = await transport.Initialize(new HostSettings(ReceiverQueue, ReceiverQueue, new StartupDiagnosticEntries(),
                (msg, ex, ct) => { }, true), new[]
            {
                new ReceiveSettings(ReceiverQueue, ReceiverQueue, true, true, "error")
            }, AdditionalReceiverQueues.Concat(new[] { ErrorQueue }).ToArray(), CancellationToken.None);

            messageDispatcher = infra.Dispatcher;
            messagePump = infra.Receivers[ReceiverQueue];
            subscriptionManager = messagePump.Subscriptions;

            await messagePump.Initialize(new PushRuntimeSettings(MaximumConcurrency),
                (messageContext, ct) =>
                {
                    receivedMessages.Add(new IncomingMessage(messageContext.MessageId, messageContext.Headers,
                        messageContext.Body), ct);
                    return Task.CompletedTask;
                }, (errorContext, ct) => Task.FromResult(ErrorHandleResult.Handled),
                CancellationToken.None
            );

            await messagePump.StartReceive(CancellationToken.None);
        }

        [TearDown]
        public async Task TearDown()
        {
            if (messagePump != null)
            {
                await messagePump.StopReceive(CancellationToken.None);
            }

            if (infra != null)
            {
                await infra.Shutdown(CancellationToken.None);
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

        protected const string ReceiverQueue = "testreceiver";
        protected const string ErrorQueue = "error";
        protected ConnectionFactory connectionFactory;
        protected IMessageDispatcher messageDispatcher;
        protected IMessageReceiver messagePump;
        protected ISubscriptionManager subscriptionManager;

        BlockingCollection<IncomingMessage> receivedMessages;

        static readonly TimeSpan IncomingMessageTimeout = TimeSpan.FromSeconds(1);
        TransportInfrastructure infra;
    }
}
