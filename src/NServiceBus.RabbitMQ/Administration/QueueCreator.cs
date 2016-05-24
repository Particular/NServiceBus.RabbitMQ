namespace NServiceBus.Transport.RabbitMQ
{
    using System.Threading.Tasks;
    using Transports;

    class QueueCreator : ICreateQueues
    {
        readonly ConnectionFactory connectionFactory;
        readonly IRoutingTopology topology;
        readonly bool durableMessagesEnabled;

        public QueueCreator(ConnectionFactory connectionFactory, IRoutingTopology topology, bool durableMessagesEnabled)
        {
            this.connectionFactory = connectionFactory;
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
            using (var connection = connectionFactory.CreateAdministrationConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(receivingAddress, durableMessagesEnabled, false, false, null);

                topology.Initialize(channel, receivingAddress);
            }
        }
    }
}