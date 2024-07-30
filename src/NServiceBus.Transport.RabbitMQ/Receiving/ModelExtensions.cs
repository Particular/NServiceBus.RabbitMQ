namespace NServiceBus.Transport.RabbitMQ
{
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Exceptions;

    static class ModelExtensions
    {
        public static ValueTask BasicAckSingle(this IChannel channel, ulong deliveryTag, CancellationToken cancellationToken = default) =>
            channel.BasicAckAsync(deliveryTag, false, cancellationToken);

        public static async Task BasicRejectAndRequeueIfOpen(this IChannel channel, ulong deliveryTag, CancellationToken cancellationToken = default)
        {
            if (channel.IsOpen)
            {
                try
                {
                    await channel.BasicRejectAsync(deliveryTag, true, cancellationToken).ConfigureAwait(false);
                }
                catch (AlreadyClosedException)
                {
                }
            }
        }
    }
}
