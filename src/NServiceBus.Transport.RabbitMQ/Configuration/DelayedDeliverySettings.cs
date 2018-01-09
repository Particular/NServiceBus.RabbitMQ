namespace NServiceBus.Transport.RabbitMQ
{
    using Configuration.AdvancedExtensibility;
    using Settings;

    /// <summary>
    /// The delayed delivery settings.
    /// </summary>
    public partial class DelayedDeliverySettings : ExposeSettings
    {
        internal DelayedDeliverySettings(SettingsHolder settings) : base(settings) { }

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
