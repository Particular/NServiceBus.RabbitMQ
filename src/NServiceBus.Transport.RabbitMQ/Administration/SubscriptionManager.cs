namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Threading.Tasks;
    using Extensibility;

    class SubscriptionManager : IManageSubscriptions
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

        public Task Subscribe(Type eventType, ContextBag context)
        {
            using (var connection = connectionFactory.CreateAdministrationConnection())
            using (var channel = new ModelWithValidation(connection.CreateModel()))
            {
                routingTopology.SetupSubscription(channel, eventType, localQueue);
            }

            return Task.CompletedTask;
        }

        public Task Unsubscribe(Type eventType, ContextBag context)
        {
            using (var connection = connectionFactory.CreateAdministrationConnection())
            using (var channel = new ModelWithValidation(connection.CreateModel()))
            {
                routingTopology.TeardownSubscription(channel, eventType, localQueue);
            }

            return Task.CompletedTask;
        }
    }
}