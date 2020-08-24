namespace NServiceBus.Transport.RabbitMQ
{
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Exceptions;

    static class ModelExtensions
    {
        class MessageState
        {
            public IModel Channel { get; set; }

            public ulong DeliveryTag { get; set; }
        }

        public static Task BasicAckSingle(this IModel channel, ulong deliveryTag) =>
            TaskEx.StartNew(
                new MessageState { Channel = channel, DeliveryTag = deliveryTag },
                state =>
                {
                    var messageState = (MessageState)state;
                    messageState.Channel.BasicAck(messageState.DeliveryTag, false);
                },
                TaskScheduler.Default);

        public static Task BasicRejectAndRequeueIfOpen(this IModel channel, ulong deliveryTag) =>
            TaskEx.StartNew(
                new MessageState { Channel = channel, DeliveryTag = deliveryTag },
                state =>
                {
                    var messageState = (MessageState)state;

                    if (messageState.Channel.IsOpen)
                    {
                        try
                        {
                            messageState.Channel.BasicReject(messageState.DeliveryTag, true);
                        }
                        catch (AlreadyClosedException)
                        {
                        }
                    }
                },
                TaskScheduler.Default);
    }
}
