namespace NServiceBus.Transports.RabbitMQ.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Transactions;
    using System.Threading.Tasks;
    using Config;
    using global::RabbitMQ.Client;
    using NServiceBus.Pipeline.Contexts;
    using NServiceBus.Support;
    using NServiceBus.Transports.RabbitMQ.Connection;
    using NUnit.Framework;
    using ObjectBuilder.Common;
    using Routing;
    using TransactionSettings = Unicast.Transport.TransactionSettings;
    using Settings;

    class RabbitMqContext
    {
        protected void MakeSureQueueAndExchangeExists(string queueName)
        {
            using (var channel = connectionManager.GetAdministrationConnection().CreateModel())
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
            var connection = connectionManager.GetAdministrationConnection();
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
            connectionManager = new RabbitMqConnectionManager(connectionFactory, config);

            publishChannel = connectionManager.GetPublishConnection().CreateModel();

            var channelProvider = new FakeChannelProvider(publishChannel);

            messageSender = new RabbitMqMessageSender(routingTopology, channelProvider, new Callbacks(new SettingsHolder()));

            messagePump = new RabbitMqMessagePump(connectionManager, routingTopology, channelProvider,
                new ReceiveOptions(s => SecondaryReceiveSettings.Enabled(CallbackQueue, 1), new MessageConverter(), 1, 1000, false, "Unit test"));

            MakeSureQueueAndExchangeExists(ReceiverQueue);

            subscriptionManager = new RabbitMqSubscriptionManager(connectionManager, routingTopology, ReceiverQueue);

            //commented out for now while sorting it all out
            //messagePump.Init()


            //    var pushSettings = new PushSettings(ReceiverQueue, Er,, TransportTransactionMode.)


            //messagePump.Init(Address.Parse(ReceiverQueue), new TransactionSettings(true, TimeSpan.FromSeconds(30), IsolationLevel.ReadCommitted, 5, false, false), m =>
            //{
            //    receivedMessages.Add(m);
            //    return true;
            //}, (s, exception) => { });

            messagePump.Start(new PushRuntimeSettings(MaximumConcurrency));
        }


        [TearDown]
        public async Task TearDown()
        {
            if (messagePump != null)
            {
                await messagePump.Stop();
            }

            publishChannel.Close();
            publishChannel.Dispose();

            connectionManager.Dispose();
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

        IModel publishChannel;
        protected string CallbackQueue = "testreceiver." + RuntimeEnvironment.MachineName;

        protected const string ReceiverQueue = "testreceiver";
        protected RabbitMqMessageSender messageSender;
        protected RabbitMqConnectionManager connectionManager;
        protected RabbitMqMessagePump messagePump;
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

    class FakeChannelProvider:IChannelProvider
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