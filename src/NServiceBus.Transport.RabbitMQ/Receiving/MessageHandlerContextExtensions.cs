namespace NServiceBus
{
    using RabbitMQ.Client.Events;

    /// <summary>
    /// Extensions to <see cref="IMessageHandlerContext"/>.
    /// </summary>
    public static class MessageHandlerContextExtensions
    {
        /// <summary>
        /// Gets the <see cref="BasicDeliverEventArgs"/> for the current message being processed.
        /// </summary>
        public static BasicDeliverEventArgs GetBasicDeliverEventArgs(this IMessageHandlerContext context) =>
            context.Extensions.Get<BasicDeliverEventArgs>();
    }
}
