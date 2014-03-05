﻿namespace NServiceBus.Transports.RabbitMQ.Tests
{
    using System;
    using System.Collections.Concurrent;
    using Config;
    using EasyNetQ;
    using NUnit.Framework;
    using Routing;
    using Unicast.Transport;

    public class RabbitMqContext
    {
        protected void MakeSureQueueAndExchangeExists(string queueName)
        {
            using (var channel = connectionManager.GetAdministrationConnection().CreateModel())
            {
                channel.QueueDeclare(queueName, true, false, false, null);
                channel.QueuePurge(queueName);

                //to make sure we kill old subscriptions
                DeleteExchange(queueName);

                routingTopology.Initialize(channel,queueName);
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

            unitOfWork = new RabbitMqUnitOfWork { ConnectionManager = connectionManager,UsePublisherConfirms = true,MaxWaitTimeForConfirms = TimeSpan.FromSeconds(10) };

            sender = new RabbitMqMessageSender { UnitOfWork = unitOfWork, RoutingTopology = routingTopology };


            dequeueStrategy = new RabbitMqDequeueStrategy { ConnectionManager = connectionManager, PurgeOnStartup = true };
            
            MakeSureQueueAndExchangeExists(ReceiverQueue);

            

            MessagePublisher = new RabbitMqMessagePublisher
                {
                    UnitOfWork = unitOfWork,
                    RoutingTopology = routingTopology
                };
            subscriptionManager = new RabbitMqSubscriptionManager
            {
                ConnectionManager = connectionManager,
                EndpointQueueName = ReceiverQueue,
                RoutingTopology = routingTopology
            };

            dequeueStrategy.Init(Address.Parse(ReceiverQueue), TransactionSettings.Default, m =>
            {
                receivedMessages.Add(m);
                return true;
            }, (s, exception) => { });

            dequeueStrategy.Start(1);
        }


        [TearDown]
        public void TearDown()
        {
            if (dequeueStrategy != null)
                dequeueStrategy.Stop();
            
            connectionManager.Dispose();
        }

        protected virtual string ExchangeNameConvention(Address address,Type eventType)
        {
            return "amq.topic";
        }


        protected TransportMessage WaitForMessage()
        {
            var waitTime = TimeSpan.FromSeconds(1);

            if (System.Diagnostics.Debugger.IsAttached)
                waitTime = TimeSpan.FromMinutes(10);

            TransportMessage message;
            receivedMessages.TryTake(out message, waitTime);

            return message;

        }

        BlockingCollection<TransportMessage> receivedMessages;

        protected ConventionalRoutingTopology routingTopology;
        protected const string ReceiverQueue = "testreceiver";
        protected RabbitMqDequeueStrategy dequeueStrategy;
        protected RabbitMqConnectionManager connectionManager;
        protected RabbitMqMessageSender sender;
        protected RabbitMqMessagePublisher MessagePublisher;
        protected RabbitMqSubscriptionManager subscriptionManager;
        protected RabbitMqUnitOfWork unitOfWork;
    }
}