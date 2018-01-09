namespace NServiceBus.Transport.RabbitMQ
{
    using Configuration.AdvancedExtensibility;
    using Settings;

    /// <summary>
    /// The delayed delivery settings.
    /// </summary>
    public class DelayedDeliverySettings : ExposeSettings
    {
        internal DelayedDeliverySettings(SettingsHolder settings) : base(settings) { }

        /// <summary>
        /// Disables the timeout manager for this endpoint.
        /// <para>
        /// The timeout manager can be disabled once all preexisting timeouts stored in the persistence for this endpoint have expired.
        /// </para>
        /// </summary>
        public DelayedDeliverySettings DisableTimeoutManager()
        {
            this.GetSettings().Set(SettingsKeys.DisableTimeoutManager, true);

            return this;
        }

        /// <summary>
        /// Enables the timeout manager for this endpoint.
        /// <para>
        /// If the timeout manager is enabled, any preexisting timeouts stored in the persistence for this endpoint will be delivered.
        /// </para>
        /// </summary>
        public DelayedDeliverySettings EnableTimeoutManager()
        {
            this.GetSettings().Set(SettingsKeys.EnableTimeoutManager, true);

            return this;
        }
    }
}
