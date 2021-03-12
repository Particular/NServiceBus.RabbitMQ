﻿
namespace NServiceBus.Transport.RabbitMQ
{
    using System.Threading;
    using Unicast.Messages;
    using System.Threading.Tasks;
    using Extensibility;

    class SubscriptionManager : ISubscriptionManager
    {
        readonly ConnectionFactory connectionFactory;
        readonly IRoutingTopology routingTopology;
        readonly string localQueue;

        public SubscriptionManager(ConnectionFactory connectionFactory, IRoutingTopology routingTopology, string localQueue)
        {
            this.connectionFactory = connectionFactory;
            this.routingTopology = routingTopology;
            this.localQueue = localQueue;
        }

        public Task SubscribeAll(MessageMetadata[] eventTypes, ContextBag context, CancellationToken cancellationToken = default)
        {
            using (var connection = connectionFactory.CreateAdministrationConnection())
            using (var channel = connection.CreateModel())
            {
                foreach (var eventType in eventTypes)
                {
                    routingTopology.SetupSubscription(channel, eventType, localQueue);
                }
            }
            return Task.CompletedTask;
        }

        public Task Unsubscribe(MessageMetadata eventType, ContextBag context, CancellationToken cancellationToken = default)
        {
            using (var connection = connectionFactory.CreateAdministrationConnection())
            using (var channel = connection.CreateModel())
            {
                routingTopology.TeardownSubscription(channel, eventType, localQueue);
            }

            return Task.CompletedTask;
        }
    }
}