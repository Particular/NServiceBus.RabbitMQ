namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.Extensibility;
    using Routing;

    class RabbitMqSubscriptionManager : IManageSubscriptions
    {
        readonly IManageRabbitMqConnections connectionManager;
        readonly IRoutingTopology routingTopology;
        readonly string localQueue;

        public RabbitMqSubscriptionManager(IManageRabbitMqConnections connectionManager, IRoutingTopology routingTopology, string localQueue)
        {
            this.connectionManager = connectionManager;
            this.routingTopology = routingTopology;
            this.localQueue = localQueue;
        }

        public Task Subscribe(Type eventType, ContextBag context)
        {
            using (var connection = connectionManager.GetAdministrationConnection())
            using (var channel = connection.CreateModel())
            {
                routingTopology.SetupSubscription(channel, eventType, localQueue);
            }

            return TaskEx.Completed;
        }

        public Task Unsubscribe(Type eventType, ContextBag context)
        {
            using (var connection = connectionManager.GetAdministrationConnection())
            using (var channel = connection.CreateModel())
            {
                routingTopology.TeardownSubscription(channel, eventType, localQueue);
            }

            return TaskEx.Completed;
        }
    }
}