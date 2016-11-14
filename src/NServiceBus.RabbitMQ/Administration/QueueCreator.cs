namespace NServiceBus.Transport.RabbitMQ
{
    using System.Linq;
    using System.Threading.Tasks;

    class QueueCreator : ICreateQueues
    {
        readonly ConnectionFactory connectionFactory;
        readonly IRoutingTopology routingTopology;

        public QueueCreator(ConnectionFactory connectionFactory, IRoutingTopology routingTopology)
        {
            this.connectionFactory = connectionFactory;
            this.routingTopology = routingTopology;
        }

        public Task CreateQueueIfNecessary(QueueBindings queueBindings, string identity)
        {
            using (var connection = connectionFactory.CreateAdministrationConnection())
            using (var channel = connection.CreateModel())
            {
                routingTopology.Initialize(channel, queueBindings.ReceivingAddresses.Concat(queueBindings.SendingAddresses));
            }

            return TaskEx.CompletedTask;
        }
    }
}