namespace NServiceBus.Transports.RabbitMQ
{
    using System.Threading.Tasks;
    using Routing;

    class RabbitMqQueueCreator : ICreateQueues
    {
        readonly IManageRabbitMqConnections connections;
        readonly IRoutingTopology topology;
        readonly bool durableMessagesEnabled;

        public RabbitMqQueueCreator(IManageRabbitMqConnections connections, IRoutingTopology topology, bool durableMessagesEnabled)
        {
            this.connections = connections;
            this.topology = topology;
            this.durableMessagesEnabled = durableMessagesEnabled;
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

            return TaskEx.Completed;
        }

        void CreateQueueIfNecessary(string receivingAddress)
        {
            using (var connection = connections.GetAdministrationConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(receivingAddress, durableMessagesEnabled, false, false, null);

                topology.Initialize(channel, receivingAddress);
            }
        }
    }
}