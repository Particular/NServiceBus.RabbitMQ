namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System.Collections.Generic;
    using global::RabbitMQ.Client;

    static class ChannelExtensions
    {
        public static void DeclareQuorumQueue(this IModel channel, string queueName)
        {
            channel.QueueDeclare(queueName, true, false, false, new Dictionary<string, object>
            {
                { "x-queue-type", "quorum" }
            });
        }

        public static void DeclareClassicQueue(this IModel channel, string queueName)
        {
            channel.QueueDeclare(queueName, true, false, false);
        }
    }
}