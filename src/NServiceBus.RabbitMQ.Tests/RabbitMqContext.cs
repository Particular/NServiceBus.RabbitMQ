namespace NServiceBus.Transports.RabbitMQ.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Config;
    using global::RabbitMQ.Client;
    using NServiceBus.Support;
    using NServiceBus.Transports.RabbitMQ.Connection;
    using NUnit.Framework;
    using ObjectBuilder.Common;
    using Routing;
    using Settings;

    class RabbitMqContext
    {
        protected void MakeSureQueueAndExchangeExists(string queueName)
        {
            using (var connection = connectionManager.GetAdministrationConnection())
            using (var channel = connection.CreateModel())
            {
                //create main q
                channel.QueueDeclare(queueName, true, false, false, null);
                channel.QueuePurge(queueName);

                //create callback q
                channel.QueueDeclare(CallbackQueue, true, false, false, null);
                channel.QueuePurge(CallbackQueue);

                //to make sure we kill old subscriptions
                DeleteExchange(queueName);

                routingTopology.Initialize(channel, queueName);
            }
        }

        void DeleteExchange(string exchangeName)
        {
            using (var connection = connectionManager.GetAdministrationConnection())
            using (var channel = connection.CreateModel())
            {
                try
                {
                    channel.ExchangeDelete(exchangeName);
                }
                // ReSharper disable EmptyGeneralCatchClause
                catch (Exception)
                // ReSharper restore EmptyGeneralCatchClause
                {
                }
            }
        }

        public virtual int MaximumConcurrency
        {
            get { return 1; }
        }

        [SetUp]
        public void SetUp()
        {
            routingTopology = new ConventionalRoutingTopology(true);
            receivedMessages = new BlockingCollection<IncomingMessage>();

            var config = new ConnectionConfiguration();
            config.ParseHosts("localhost:5672");

            var connectionFactory = new RabbitMqConnectionFactory(config);
            connectionManager = new RabbitMqConnectionManager(connectionFactory);
            var channelProvider = new ChannelProvider(connectionManager, config.UsePublisherConfirms, config.MaxWaitTimeForConfirms);

            var settingsHolder = new SettingsHolder();
            settingsHolder.Set("NServiceBus.LocalAddress", ReceiverQueue);
            messageSender = new RabbitMqMessageSender(routingTopology, channelProvider);

            var purger = new QueuePurger(connectionManager);
            var poisonMessageForwarder = new PoisonMessageForwarder(channelProvider, routingTopology);

            messagePump = new MessagePump(
                new ReceiveOptions(s => SecondaryReceiveSettings.Enabled(CallbackQueue, 1), new MessageConverter(), 1, 1000, false, "Unit test"),
                config,
                poisonMessageForwarder,
                purger);

            MakeSureQueueAndExchangeExists(ReceiverQueue);

            subscriptionManager = new RabbitMqSubscriptionManager(connectionManager, routingTopology, ReceiverQueue);

            messagePump.Init(pushContext =>
            {
                receivedMessages.Add(new IncomingMessage(pushContext.MessageId, pushContext.Headers, pushContext.BodyStream));
                return TaskEx.Completed;
            },
                new CriticalError(_ => TaskEx.Completed),
                new PushSettings(ReceiverQueue, "error", true, TransportTransactionMode.ReceiveOnly)
            ).GetAwaiter().GetResult();

            messagePump.Start(new PushRuntimeSettings(MaximumConcurrency));
        }


        [TearDown]
        public void TearDown()
        {
            messagePump?.Stop().GetAwaiter().GetResult();

            connectionManager?.Dispose();
        }

        protected virtual string ExchangeNameConvention()
        {
            return "amq.topic";
        }


        protected IncomingMessage WaitForMessage()
        {
            var waitTime = TimeSpan.FromSeconds(1);

            if (Debugger.IsAttached)
            {
                waitTime = TimeSpan.FromMinutes(10);
            }

            IncomingMessage message;
            receivedMessages.TryTake(out message, waitTime);

            return message;
        }

        protected string CallbackQueue = "testreceiver." + RuntimeEnvironment.MachineName;

        protected const string ReceiverQueue = "testreceiver";
        protected RabbitMqMessageSender messageSender;
        protected RabbitMqConnectionManager connectionManager;
        protected MessagePump messagePump;
        BlockingCollection<IncomingMessage> receivedMessages;

        protected ConventionalRoutingTopology routingTopology;
        protected RabbitMqSubscriptionManager subscriptionManager;
    }

    class FakeContainer : IContainer
    {
        public void Dispose()
        {
        }

        public object Build(Type typeToBuild)
        {
            throw new NotImplementedException();
        }

        public IContainer BuildChildContainer()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<object> BuildAll(Type typeToBuild)
        {
            throw new NotImplementedException();
        }

        public void Configure(Type component, DependencyLifecycle dependencyLifecycle)
        {

        }

        public void Configure<T>(Func<T> component, DependencyLifecycle dependencyLifecycle)
        {

        }

        public void ConfigureProperty(Type component, string property, object value)
        {

        }

        public void RegisterSingleton(Type lookupType, object instance)
        {

        }

        public bool HasComponent(Type componentType)
        {
            throw new NotImplementedException();
        }

        public void Release(object instance)
        {

        }
    }

    class FakeChannelProvider : IChannelProvider
    {
        readonly IModel publishChannel;

        public FakeChannelProvider(IModel publishChannel)
        {
            this.publishChannel = publishChannel;
        }

        public bool TryGetPublishChannel(out IModel channel)
        {
            channel = publishChannel;

            return true;
        }

        public ConfirmsAwareChannel GetNewPublishChannel()
        {
            throw new NotImplementedException();
        }
    }
}
