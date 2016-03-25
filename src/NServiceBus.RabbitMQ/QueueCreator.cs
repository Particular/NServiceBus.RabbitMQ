namespace NServiceBus.Transports.RabbitMQ
{
    using System.Threading.Tasks;
    using Routing;

    class QueueCreator : ICreateQueues
    {
        readonly ConnectionManager connectionManager;
        readonly IRoutingTopology topology;
        readonly bool durableMessagesEnabled;

        public QueueCreator(ConnectionManager connectionManager, IRoutingTopology topology, bool durableMessagesEnabled)
        {
            this.connectionManager = connectionManager;
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

            return TaskEx.CompletedTask;
        }

        void CreateQueueIfNecessary(string receivingAddress)
        {
            using (var connection = connectionManager.GetAdministrationConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(receivingAddress, durableMessagesEnabled, false, false, null);

                topology.Initialize(channel, receivingAddress);
            }
        }
    }
}