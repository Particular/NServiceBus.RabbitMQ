namespace NServiceBus
{
    using RabbitMQ.Client.Events;
    using System;

    /// <summary>
    /// Extensions to <see cref="IMessageHandlerContext"/>.
    /// </summary>
    public static class MessageHandlerContextExtensions
    {
        /// <summary>
        /// Gets the <see cref="BasicDeliverEventArgs"/> for the current message being processed.
        /// </summary>
        public static BasicDeliverEventArgs GetBasicDeliverEventArgs(this IMessageHandlerContext context) =>
            context.Extensions.TryGet<BasicDeliverEventArgs>(out var args)
                ? args
                : throw new InvalidOperationException(
                    "BasicDeliverEventArgs have not been propagated to the message handler context." +
                    "To enable propagation, call PropagateBasicDeliverEventArgs(true) when configuring the transport.");
    }
}
