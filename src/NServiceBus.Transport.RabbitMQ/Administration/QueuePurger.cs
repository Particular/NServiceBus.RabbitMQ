namespace NServiceBus.Transport.RabbitMQ
{
    class QueuePurger
    {
        readonly AmqpConnectionFactory connectionFactory;

        public QueuePurger(AmqpConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public void Purge(string queue)
        {
            using (var connection = connectionFactory.CreateAdministrationConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueuePurge(queue);
            }
        }
    }
}
