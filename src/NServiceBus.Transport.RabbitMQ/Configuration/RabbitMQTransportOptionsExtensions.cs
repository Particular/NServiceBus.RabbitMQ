namespace NServiceBus
{
    using Transport.RabbitMQ;

    /// <summary>
    /// Adds access to the RabbitMQ transport config to varios options for outgoing operations.
    /// </summary>
    public static class RabbitMQTransportOptionsExtensions
    {
        /// <summary>
        /// Requests the message to be delivered with delivery mode set to non-persistent.
        /// </summary>
        /// <param name="options">Options being extended.</param>
        public static void UseNonPersistentDeliveryMode(this SendOptions options)
        {
            Guard.AgainstNull(nameof(options), options);

            options.SetHeader(BasicPropertiesExtensions.UseNonPersistentDeliveryHeader, bool.TrueString);
        }

        /// <summary>
        /// Requests the message to be delivered with delivery mode set to non-persistent.
        /// </summary>
        /// <param name="options">Options being extended.</param>
        public static void UseNonPersistentDeliveryMode(this PublishOptions options)
        {
            Guard.AgainstNull(nameof(options), options);

            options.SetHeader(BasicPropertiesExtensions.UseNonPersistentDeliveryHeader, bool.TrueString);
        }

        /// <summary>
        /// Requests the message to be delivered with delivery mode set to non-persistent.
        /// </summary>
        /// <param name="options">Options being extended.</param>
        public static void UseNonPersistentDeliveryMode(this ReplyOptions options)
        {
            Guard.AgainstNull(nameof(options), options);

            options.SetHeader(BasicPropertiesExtensions.UseNonPersistentDeliveryHeader, bool.TrueString);
        }
    }
}
