namespace NServiceBus.Transport.RabbitMQ
{
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
                DelayInfrastructure.Build(channel);

                routingTopology.Initialize(channel, queueBindings.ReceivingAddresses, queueBindings.SendingAddresses);

                foreach (var receivingAddress in queueBindings.ReceivingAddresses)
                {
                    routingTopology.BindToDelayInfrastructure(channel, receivingAddress, DelayInfrastructure.DeliveryExchange, DelayInfrastructure.BindingKey(receivingAddress));
                }
            }

            return Task.CompletedTask;
        }
    }
}