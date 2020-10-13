namespace NServiceBus
{
    using Transport.RabbitMQ;

    /// <summary>
    ///
    /// </summary>
    public static class RabbitMQTransportOptionsExtensions
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="options">Options being extended.</param>
        public static void UseNonPersistentDeliveryMode(this SendOptions options)
        {
            Guard.AgainstNull(nameof(options), options);

            options.SetHeader(BasicPropertiesExtensions.UseNonPersistentDeliveryHeader, bool.TrueString);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="options">Options being extended.</param>
        public static void UseNonPersistentDeliveryMode(this PublishOptions options)
        {
            Guard.AgainstNull(nameof(options), options);

            options.SetHeader(BasicPropertiesExtensions.UseNonPersistentDeliveryHeader, bool.TrueString);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="options">Options being extended.</param>
        public static void UseNonPersistentDeliveryMode(this ReplyOptions options)
        {
            Guard.AgainstNull(nameof(options), options);

            options.SetHeader(BasicPropertiesExtensions.UseNonPersistentDeliveryHeader, bool.TrueString);
        }
    }
}
