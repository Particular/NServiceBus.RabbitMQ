namespace NServiceBus.Transports.RabbitMQ
{
    using Routing;

    class RabbitMqQueueCreator : ICreateQueues
    {
        public IManageRabbitMqConnections ConnectionManager { get; set; }

        public IRoutingTopology RoutingTopology { get; set; }

        public Configure Configure { get; set; }

        public void CreateQueueIfNecessary(Address address, string account)
        {
            using (var channel = ConnectionManager.GetAdministrationConnection().CreateModel())
            {
                channel.QueueDeclare(address.Queue, Configure.DurableMessagesEnabled(), false, false, null);

                RoutingTopology.Initialize(channel, address.Queue);
            }

        }
    }
}