namespace NServiceBus.Transport.RabbitMQ
{
    using System.Threading;
    using System.Threading.Tasks;

    class QueuePurger
    {
        readonly ConnectionFactory connectionFactory;

        public QueuePurger(ConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public async Task Purge(string queue, CancellationToken cancellationToken = default)
        {
            using var connection = await connectionFactory.CreateAdministrationConnection(cancellationToken).ConfigureAwait(false);
            using var channel = await connection.CreateChannelAsync(cancellationToken).ConfigureAwait(false);
            await channel.QueuePurgeAsync(queue, cancellationToken).ConfigureAwait(false);
        }
    }
}
