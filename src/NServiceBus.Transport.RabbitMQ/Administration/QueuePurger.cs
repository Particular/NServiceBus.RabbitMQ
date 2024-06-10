namespace NServiceBus.Transport.RabbitMQ
{
    class QueuePurger
    {
        readonly ConnectionFactory connectionFactory;

        public QueuePurger(ConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public void Purge(string queue)
        {
            var (connection, unregister) = connectionFactory.CreateAdministrationConnection();
            using (connection)
            using (unregister)
            using (var channel = connection.CreateModel())
            {
                channel.QueuePurge(queue);
            }
        }
    }
}
