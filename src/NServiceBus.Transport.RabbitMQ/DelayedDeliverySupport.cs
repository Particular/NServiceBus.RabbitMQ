namespace NServiceBus
{
    /// <summary>
    /// Describes the support of delayed delivery when running RabbitMQ transport in a RabbitMQ cluster.
    /// </summary>
    public enum DelayedDeliverySupport
    {
        /// <summary>
        /// Delayed delivery functionality is disabled. No delayed delivery feature can be used.
        /// </summary>
        Disabled,
        /// <summary>
        /// Delayed delivery features can be used but are at risk of message loss. Refer to the documentation for further details.
        /// </summary>
        UnsafeEnabled
    }
}