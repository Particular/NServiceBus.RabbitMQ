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

                var queueDeclaringTopology = routingTopology as IDeclareQueues;

                if (queueDeclaringTopology != null)
                {
                    queueDeclaringTopology.DeclareAndInitialize(channel, queueBindings.ReceivingAddresses, queueBindings.SendingAddresses);
                }
                else
                {
                    foreach (var receivingAddress in queueBindings.ReceivingAddresses)
                    {
                        CreateQueueIfNecessary(channel, receivingAddress);
                    }

                    foreach (var sendingAddress in queueBindings.SendingAddresses)
                    {
                        CreateQueueIfNecessary(channel, sendingAddress);
                    }
                }

                var delayTopology = routingTopology as ISupportDelayedDelivery;

                if (delayTopology != null)
                {
                    foreach (var receivingAddress in queueBindings.ReceivingAddresses)
                    {
                        delayTopology.BindToDelayInfrastructure(channel, receivingAddress, DelayInfrastructure.DeliveryExchange, DelayInfrastructure.BindingKey(receivingAddress));
                    }
                }
            }

            return TaskEx.CompletedTask;
        }

        void CreateQueueIfNecessary(IModel channel, string receivingAddress)
        {
            channel.QueueDeclare(receivingAddress, durableMessagesEnabled, false, false, null);

            routingTopology.Initialize(channel, receivingAddress);
        }
    }
}