namespace NServiceBus.Transports.RabbitMQ
{
    using System.Threading.Tasks;
    using Routing;

    class RabbitMqQueueCreator : ICreateQueues
    {
        private readonly IManageRabbitMqConnections connections;
        private readonly IRoutingTopology topology;
        readonly Callbacks callbacks;
        readonly bool durableMessagesEnabled;

        public RabbitMqQueueCreator(IManageRabbitMqConnections connections, IRoutingTopology topology, Callbacks callbacks, bool durableMessagesEnabled)
        {
            this.connections = connections;
            this.topology = topology;
            this.callbacks = callbacks;
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

            if (callbacks.Enabled)
            {
                CreateQueueIfNecessary(callbacks.QueueAddress);
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