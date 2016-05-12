namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.Extensibility;
    using NServiceBus.Transports;

    class SubscriptionManager : IManageSubscriptions
    {
        readonly ConnectionManager connectionManager;
        readonly IRoutingTopology routingTopology;
        readonly string localQueue;

        public SubscriptionManager(ConnectionManager connectionManager, IRoutingTopology routingTopology, string localQueue)
        {
            this.connectionManager = connectionManager;
            this.routingTopology = routingTopology;
            this.localQueue = localQueue;
        }

        public Task Subscribe(Type eventType, ContextBag context)
        {
            using (var connection = connectionManager.CreateAdministrationConnection())
            using (var channel = connection.CreateModel())
            {
                routingTopology.SetupSubscription(channel, eventType, localQueue);
            }

            return TaskEx.CompletedTask;
        }

        public Task Unsubscribe(Type eventType, ContextBag context)
        {
            using (var connection = connectionManager.CreateAdministrationConnection())
            using (var channel = connection.CreateModel())
            {
                routingTopology.TeardownSubscription(channel, eventType, localQueue);
            }

            return TaskEx.CompletedTask;
        }
    }
}