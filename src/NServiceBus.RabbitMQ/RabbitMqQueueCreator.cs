namespace NServiceBus.Transports.RabbitMQ
{
    using System.Threading.Tasks;
    using NServiceBus.Settings;
    using Routing;

    class RabbitMqQueueCreator : ICreateQueues
    {
        private readonly IManageRabbitMqConnections connections;
        private readonly IRoutingTopology topology;
        private readonly ReadOnlySettings settings;

        public RabbitMqQueueCreator(IManageRabbitMqConnections connections, IRoutingTopology topology, ReadOnlySettings settings)
        {
            this.connections = connections;
            this.topology = topology;
            this.settings = settings;
        }

        public Task CreateQueueIfNecessary(QueueBindings queueBindings, string identity)
        {
            foreach (var receivingAddress in queueBindings.ReceivingAddresses)
            {
                CreateQueueIfNecessary(receivingAddress);
            }

            foreach (var sendingAddress in queueBindings.SendingAddresses)
            {
                CreateQueueIfNecessary(sendingAddress);
            }

            return Task.FromResult(0);
        }

        private void CreateQueueIfNecessary(string receivingAddress)
        {
            using (var connection = connections.GetAdministrationConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(receivingAddress, settings.DurableMessagesEnabled(), false, false, null);

                topology.Initialize(channel, receivingAddress);
            }
        }
    }
}