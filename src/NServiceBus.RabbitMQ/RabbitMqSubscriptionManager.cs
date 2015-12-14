namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.Extensibility;
    using Routing;

    class RabbitMqSubscriptionManager : IManageSubscriptions
    {
        public IManageRabbitMqConnections ConnectionManager { get; set; }

        public string EndpointQueueName { get; set; }

        public IRoutingTopology RoutingTopology { get; set; }

        public Task Subscribe(Type eventType, ContextBag context)
        {
            using (var connection = ConnectionManager.GetAdministrationConnection())
            using (var channel = connection.CreateModel())
            {
                RoutingTopology.SetupSubscription(channel, eventType, EndpointQueueName);
            }

            return Task.FromResult(0);
        }

        public Task Unsubscribe(Type eventType, ContextBag context)
        {
            using (var connection = ConnectionManager.GetAdministrationConnection())
            using (var channel = connection.CreateModel())
            {
                RoutingTopology.TeardownSubscription(channel, eventType, EndpointQueueName);
            }

            return Task.FromResult(0);
        }
    }
}