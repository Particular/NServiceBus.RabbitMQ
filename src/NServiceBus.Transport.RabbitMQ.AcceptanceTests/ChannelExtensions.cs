namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;

    static class ChannelExtensions
    {
        public static Task<QueueDeclareOk> DeclareQuorumQueue(this IChannel channel, string queueName)
        {
            return channel.QueueDeclareAsync(queueName, true, false, false, new Dictionary<string, object>
            {
                { "x-queue-type", "quorum" }
            });
        }

        public static Task<QueueDeclareOk> DeclareClassicQueue(this IChannel channel, string queueName)
        {
            return channel.QueueDeclareAsync(queueName, true, false, false);
        }
    }
}