namespace NServiceBus.Transports.RabbitMQ
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
            using (var connection = connectionManager.GetAdministrationConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueuePurge(queue);
            }
        }
    }
}
