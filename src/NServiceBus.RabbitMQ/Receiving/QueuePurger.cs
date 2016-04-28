namespace NServiceBus.Transport.RabbitMQ
{
    class QueuePurger
    {
        readonly ConnectionManager connectionManager;

        public QueuePurger(ConnectionManager connectionManager)
        {
            this.connectionManager = connectionManager;
        }

        public void Purge(string queue)
        {
            using (var connection = connectionManager.CreateAdministrationConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueuePurge(queue);
            }
        }
    }
}
