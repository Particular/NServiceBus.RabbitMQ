﻿namespace NServiceBus.Transport.RabbitMQ.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Support;

    class RabbitMqContext
    {
        readonly TimeSpan defaultRetryDelay = TimeSpan.FromSeconds(60);
        public virtual int MaximumConcurrency => 1;

        [SetUp]
        public void SetUp()
        {
            routingTopology = new ConventionalRoutingTopology(true, QueueType.Classic);
            receivedMessages = new BlockingCollection<IncomingMessage>();

            var connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString");

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new Exception("The 'RabbitMQTransport_ConnectionString' environment variable is not set.");
            }

            var config = ConnectionConfiguration.Create(connectionString, string.Empty);

            connectionFactory = new ConnectionFactory(ReceiverQueue, config, default, false, false, default, default);
            channelProvider = new ChannelProvider(connectionFactory, defaultRetryDelay, routingTopology);
            channelProvider.CreateConnection();

            messageDispatcher = new MessageDispatcher(channelProvider);

            var purger = new QueuePurger(connectionFactory);

            messagePump = new MessagePump(connectionFactory, new MessageConverter(), "Unit test", channelProvider, purger, TimeSpan.FromMinutes(2), 3, 0, defaultRetryDelay);

            routingTopology.Reset(connectionFactory, new[] { ReceiverQueue }.Concat(AdditionalReceiverQueues), new[] { ErrorQueue });

            subscriptionManager = new SubscriptionManager(connectionFactory, routingTopology, ReceiverQueue);

            messagePump.Init(messageContext =>
            {
                receivedMessages.Add(new IncomingMessage(messageContext.MessageId, messageContext.Headers, messageContext.Body));
                return Task.CompletedTask;
            },
                ErrorContext => Task.FromResult(ErrorHandleResult.Handled),
                new CriticalError(_ => Task.CompletedTask),
                new PushSettings(ReceiverQueue, ErrorQueue, true, TransportTransactionMode.ReceiveOnly)
            ).GetAwaiter().GetResult();

            messagePump.Start(new PushRuntimeSettings(MaximumConcurrency));
        }

        [TearDown]
        public void TearDown()
        {
            messagePump?.Stop().GetAwaiter().GetResult();

            channelProvider?.Dispose();
        }

        protected bool TryWaitForMessageReceipt() => TryReceiveMessage(out var _, incomingMessageTimeout);

        protected IncomingMessage ReceiveMessage()
        {
            if (!TryReceiveMessage(out var message, incomingMessageTimeout))
            {
                throw new TimeoutException($"The message did not arrive within {incomingMessageTimeout.TotalSeconds} seconds.");
            }

            return message;
        }

        bool TryReceiveMessage(out IncomingMessage message, TimeSpan timeout) =>
            receivedMessages.TryTake(out message, timeout);

        protected virtual IEnumerable<string> AdditionalReceiverQueues => Enumerable.Empty<string>();

        protected const string ReceiverQueue = "testreceiver";
        protected const string ErrorQueue = "error";
        protected MessageDispatcher messageDispatcher;
        protected ConnectionFactory connectionFactory;
        protected MessagePump messagePump;
        protected SubscriptionManager subscriptionManager;

        ChannelProvider channelProvider;
        BlockingCollection<IncomingMessage> receivedMessages;
        ConventionalRoutingTopology routingTopology;

        static readonly TimeSpan incomingMessageTimeout = TimeSpan.FromSeconds(5);
    }
}
