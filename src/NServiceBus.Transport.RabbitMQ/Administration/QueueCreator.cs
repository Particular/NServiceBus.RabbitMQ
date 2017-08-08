namespace NServiceBus.Transport.RabbitMQ
{
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;

    class QueueCreator : ICreateQueues
    {
        readonly ConnectionFactory connectionFactory;
        readonly IRoutingTopology routingTopology;
        readonly bool durableMessagesEnabled;

        public QueueCreator(ConnectionFactory connectionFactory, IRoutingTopology routingTopology, bool durableMessagesEnabled)
        {
            this.connectionFactory = connectionFactory;
            this.routingTopology = routingTopology;
            this.durableMessagesEnabled = durableMessagesEnabled;
        }

        public Task CreateQueueIfNecessary(QueueBindings queueBindings, string identity)
        {
            using (var connection = connectionFactory.CreateAdministrationConnection())
            using (var channel = connection.CreateModel())
            {
                DelayInfrastructure.Build(channel);

                routingTopology.Initialize(channel, queueBindings.ReceivingAddresses, queueBindings.SendingAddresses);

                if (routingTopology is ISupportDelayedDelivery delayTopology)
                {
                    foreach (var receivingAddress in queueBindings.ReceivingAddresses)
                    {
                        delayTopology.BindToDelayInfrastructure(channel, receivingAddress, DelayInfrastructure.DeliveryExchange, DelayInfrastructure.BindingKey(receivingAddress));
                    }
                }
            }

            return TaskEx.CompletedTask;
        }
    }
}