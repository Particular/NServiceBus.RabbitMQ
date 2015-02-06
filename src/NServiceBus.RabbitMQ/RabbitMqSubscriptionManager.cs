namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using Routing;

    class RabbitMqSubscriptionManager : IManageSubscriptions
    {
        public IManageRabbitMqConnections ConnectionManager { get; set; }

        public string EndpointQueueName { get; set; }

        public IRoutingTopology RoutingTopology { get; set; }

        public void Subscribe(Type eventType, Address publisherAddress)
        {
            using (var connection = ConnectionManager.GetAdministrationConnection())
            using (var channel = connection.CreateModel())
            {
                RoutingTopology.SetupSubscription(channel, eventType, EndpointQueueName);
            }
        }

        public void Unsubscribe(Type eventType, Address publisherAddress)
        {
            using (var connection = ConnectionManager.GetAdministrationConnection())
            using (var channel = connection.CreateModel())
            {
                RoutingTopology.TeardownSubscription(channel, eventType, EndpointQueueName);
            }
        }
    }
}