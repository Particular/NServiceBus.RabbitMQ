namespace NServiceBus.Transports.RabbitMQ.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Transactions;
    using Config;
    using EasyNetQ;
    using global::RabbitMQ.Client;
    using NUnit.Framework;
    using Routing;
    using Unicast.Transport;

    class RabbitMqContext
    {
        protected void MakeSureQueueAndExchangeExists(string queueName)
        {
            using (var channel = connectionManager.GetAdministrationConnection().CreateModel())
            {
                channel.QueueDeclare(queueName, true, false, false, null);
                channel.QueuePurge(queueName);

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
            routingTopology = new ConventionalRoutingTopology();
            receivedMessages = new BlockingCollection<TransportMessage>();

            var config = new ConnectionConfiguration();
            config.ParseHosts("localhost:5672");

            var selectionStrategy = new DefaultClusterHostSelectionStrategy<ConnectionFactoryInfo>();
            var connectionFactory = new ConnectionFactoryWrapper(config, selectionStrategy);
            connectionManager = new RabbitMqConnectionManager(connectionFactory, config);

            publishChannel = connectionManager.GetPublishConnection().CreateModel();

            var channelProvider = new FakeChannelProvider(publishChannel);

            sender = new RabbitMqMessageSender
            {
                ChannelProvider = channelProvider,
                RoutingTopology = routingTopology
            };


            dequeueStrategy = new RabbitMqDequeueStrategy(connectionManager, null)
            {
                PurgeOnStartup = true
            };

            MakeSureQueueAndExchangeExists(ReceiverQueue);


            MessagePublisher = new RabbitMqMessagePublisher
            {
                ChannelProvider = channelProvider,
                RoutingTopology = routingTopology
            };
            subscriptionManager = new RabbitMqSubscriptionManager
            {
                ConnectionManager = connectionManager,
                EndpointQueueName = ReceiverQueue,
                RoutingTopology = routingTopology
            };

            dequeueStrategy.Init(Address.Parse(ReceiverQueue), new TransactionSettings(true, TimeSpan.FromSeconds(30), IsolationLevel.ReadCommitted, 5, false, false), m =>
            {
                receivedMessages.Add(m);
                return true;
            }, (s, exception) => { });

            dequeueStrategy.Start(MaximumConcurrency);
        }


        [TearDown]
        public void TearDown()
        {
            if (dequeueStrategy != null)
            {
                dequeueStrategy.Stop();
            }

            publishChannel.Close();
            publishChannel.Dispose();

            connectionManager.Dispose();
        }

        protected virtual string ExchangeNameConvention()
        {
            return "amq.topic";
        }


        protected TransportMessage WaitForMessage()
        {
            var waitTime = TimeSpan.FromSeconds(1);

            if (Debugger.IsAttached)
            {
                waitTime = TimeSpan.FromMinutes(10);
            }

            TransportMessage message;
            receivedMessages.TryTake(out message, waitTime);

            return message;
        }

        IModel publishChannel;

        protected const string ReceiverQueue = "testreceiver";
        protected RabbitMqMessagePublisher MessagePublisher;
        protected RabbitMqConnectionManager connectionManager;
        protected RabbitMqDequeueStrategy dequeueStrategy;
        BlockingCollection<TransportMessage> receivedMessages;

        protected ConventionalRoutingTopology routingTopology;
        protected RabbitMqMessageSender sender;
        protected RabbitMqSubscriptionManager subscriptionManager;
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