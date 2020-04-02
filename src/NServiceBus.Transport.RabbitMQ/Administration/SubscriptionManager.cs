namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Threading.Tasks;
    using Extensibility;

    class SubscriptionManager : IManageSubscriptions
    {
        readonly AmqpConnectionFactory connectionFactory;
        readonly IRoutingTopology routingTopology;
        readonly string localQueue;

        public SubscriptionManager(AmqpConnectionFactory connectionFactory, IRoutingTopology routingTopology, string localQueue)
        {
            this.connectionFactory = connectionFactory;
            this.routingTopology = routingTopology;
            this.localQueue = localQueue;
        }

        public Task Subscribe(Type eventType, ContextBag context)
        {
            using (var connection = connectionFactory.CreateAdministrationConnection())
            using (var channel = connection.CreateModel())
            {
                routingTopology.SetupSubscription(channel, eventType, localQueue);
            }

            return TaskEx.CompletedTask;
        }

        public Task Unsubscribe(Type eventType, ContextBag context)
        {
            using (var connection = connectionFactory.CreateAdministrationConnection())
            using (var channel = connection.CreateModel())
            {
                routingTopology.TeardownSubscription(channel, eventType, localQueue);
            }

            return TaskEx.CompletedTask;
        }
    }
}