namespace NServiceBus.Transport.RabbitMQ
{
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;

    static class ModelExtensions
    {
        class MessageState
        {
            public IModel Channel { get; set; }

            public ulong DeliveryTag { get; set; }
        }

        public static Task BasicAckSingle(this IModel channel, ulong deliveryTag, TaskScheduler scheduler) =>
            TaskEx.StartNew(
                new MessageState { Channel = channel, DeliveryTag = deliveryTag },
                state =>
                {
                    var messageState = (MessageState)state;
                    messageState.Channel.BasicAck(messageState.DeliveryTag, false);
                },
                scheduler);

        public static Task BasicRejectAndRequeue(this IModel channel, ulong deliveryTag, TaskScheduler scheduler) =>
            TaskEx.StartNew(
                new MessageState { Channel = channel, DeliveryTag = deliveryTag },
                state =>
                {
                    var messageState = (MessageState)state;
                    messageState.Channel.BasicReject(messageState.DeliveryTag, true);
                },
                scheduler);
    }
}
