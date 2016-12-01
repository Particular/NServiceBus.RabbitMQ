﻿namespace NServiceBus.Transport.RabbitMQ.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Linq;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Support;

    class RabbitMqContext
    {
        public virtual int MaximumConcurrency => 1;

        [SetUp]
        public void SetUp()
        {
            routingTopology = new ConventionalRoutingTopology(true);
            receivedMessages = new BlockingCollection<IncomingMessage>();

            var connectionStringBuilder = new DbConnectionStringBuilder
            {
                ConnectionString = Environment.GetEnvironmentVariable("RabbitMQTransport.ConnectionString")
            };

            ApplyDefault(connectionStringBuilder, "username", "guest");
            ApplyDefault(connectionStringBuilder, "password", "guest");
            ApplyDefault(connectionStringBuilder, "virtualhost", "nsb-rabbitmq-test");
            ApplyDefault(connectionStringBuilder, "host", "localhost");

            ConnectionConfiguration config;

            var parser = new ConnectionStringParser(ReceiverQueue);
            config = parser.Parse(connectionStringBuilder.ConnectionString);

            connectionFactory = new ConnectionFactory(config, null);
            channelProvider = new ChannelProvider(connectionFactory, routingTopology, true);

            messageDispatcher = new MessageDispatcher(channelProvider);

            var purger = new QueuePurger(connectionFactory);

            messagePump = new MessagePump(connectionFactory, new MessageConverter(), "Unit test", channelProvider, purger, TimeSpan.FromMinutes(2), 3, 0);

            routingTopology.Reset(connectionFactory, new[] { ReceiverQueue }.Concat(AdditionalReceiverQueues), new[] { ErrorQueue });

            subscriptionManager = new SubscriptionManager(connectionFactory, routingTopology, ReceiverQueue);

            messagePump.Init(messageContext =>
            {
                receivedMessages.Add(new IncomingMessage(messageContext.MessageId, messageContext.Headers, messageContext.Body));
                return TaskEx.CompletedTask;
            },
                ErrorContext => Task.FromResult(ErrorHandleResult.Handled),
                new CriticalError(_ => TaskEx.CompletedTask),
                new PushSettings(ReceiverQueue, ErrorQueue, true, TransportTransactionMode.ReceiveOnly)
            ).GetAwaiter().GetResult();

            messagePump.Start(new PushRuntimeSettings(MaximumConcurrency));
        }

        static void ApplyDefault(DbConnectionStringBuilder builder, string key, string value)
        {
            if (!builder.ContainsKey(key))
            {
                builder.Add(key, value);
            }
        }

        [TearDown]
        public void TearDown()
        {
            messagePump?.Stop().GetAwaiter().GetResult();

            channelProvider?.Dispose();
        }

        protected bool TryWaitForMessageReceipt()
        {
            IncomingMessage message;
            return TryReceiveMessage(out message, incomingMessageTimeout);
        }

        protected IncomingMessage ReceiveMessage()
        {
            IncomingMessage message;
            if (!TryReceiveMessage(out message, incomingMessageTimeout))
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

        static readonly TimeSpan incomingMessageTimeout = TimeSpan.FromSeconds(1);
    }
}
