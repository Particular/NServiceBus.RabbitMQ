namespace NServiceBus.Transports.RabbitMQ
{
    using Routing;
    using Settings;

    public class RabbitMqQueueCreator : ICreateQueues
    {
        public IManageRabbitMqConnections ConnectionManager { get; set; }

        public IRoutingTopology RoutingTopology { get; set; }

        public void CreateQueueIfNecessary(Address address, string account)
        {
            var durable = SettingsHolder.Get<bool>("Endpoint.DurableMessages");

            using (var channel = ConnectionManager.GetAdministrationConnection().CreateModel())
            {
                channel.QueueDeclare(address.Queue, durable, false, false, null);

                RoutingTopology.Initialize(channel, address.Queue);
            }

        }
    }
}