namespace NServiceBus.Transports.RabbitMQ
{
    class QueuePurger
    {
        readonly IManageRabbitMqConnections connectionManager;

        public QueuePurger(IManageRabbitMqConnections connectionManager)
        {
            this.connectionManager = connectionManager;
        }

        public void Purge(string queue)
        {
            using (var connection = connectionManager.GetAdministrationConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueuePurge(queue);
            }
        }
    }
}