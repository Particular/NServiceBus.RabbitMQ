namespace NServiceBus.Transport.RabbitMQ
{
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Exceptions;

    static class ModelExtensions
    {
        public static void BasicAckSingle(this IModel channel, ulong deliveryTag) =>
            channel.BasicAck(deliveryTag, false);

        public static void BasicRejectAndRequeueIfOpen(this IModel channel, ulong deliveryTag)
        {
            if (channel.IsOpen)
            {
                try
                {
                    channel.BasicReject(deliveryTag, true);
                }
                catch (AlreadyClosedException)
                {
                }
            }
        }
    }
}
