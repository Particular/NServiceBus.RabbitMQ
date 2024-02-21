namespace NServiceBus
{
    using System;
    using Transport.RabbitMQ;

    /// <summary>
    /// Adds extension methods for the relevant ExtendableOptions classes.
    /// </summary>
    public static class NonPersistentDeliveryModeExtensions
    {
        /// <summary>
        /// Uses the non-persistent delivery mode to send the message.
        /// </summary>
        public static void UseNonPersistentDeliveryMode(this SendOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            options.SetHeader(BasicPropertiesExtensions.UseNonPersistentDeliveryHeader, bool.TrueString);
        }

        /// <summary>
        /// Uses the non-persistent delivery mode to publish the message.
        /// </summary>
        public static void UseNonPersistentDeliveryMode(this PublishOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            options.SetHeader(BasicPropertiesExtensions.UseNonPersistentDeliveryHeader, bool.TrueString);
        }

        /// <summary>
        /// Uses the non-persistent delivery mode to send the reply.
        /// </summary>
        public static void UseNonPersistentDeliveryMode(this ReplyOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            options.SetHeader(BasicPropertiesExtensions.UseNonPersistentDeliveryHeader, bool.TrueString);
        }
    }
}
