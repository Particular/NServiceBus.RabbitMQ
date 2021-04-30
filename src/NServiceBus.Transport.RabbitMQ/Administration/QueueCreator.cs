namespace NServiceBus.Transport.RabbitMQ
{
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;

    class QueueCreator : ICreateQueues
    {
        readonly ConnectionFactory connectionFactory;
        readonly IRoutingTopology routingTopology;

        public static ThreadLocal<IConnection> RoutingTopoligyInitializeConnection = new ThreadLocal<IConnection>();

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

                try
                {
                    // workaround to make the connection available to the routing topology
                    RoutingTopoligyInitializeConnection.Value = connection;
                    routingTopology.Initialize(channel, queueBindings.ReceivingAddresses, queueBindings.SendingAddresses);
                }
                finally
                {
                    RoutingTopoligyInitializeConnection.Value = null;
                }

                foreach (var receivingAddress in queueBindings.ReceivingAddresses)
                {
                    routingTopology.BindToDelayInfrastructure(channel, receivingAddress, DelayInfrastructure.DeliveryExchange, DelayInfrastructure.BindingKey(receivingAddress));
                }
            }

            return Task.CompletedTask;
        }
    }
}