namespace NServiceBus
{
    /// <summary>
    /// Described the support of timeouts when running RabbitMQ transport in a RabbitMQ cluster.
    /// </summary>
    public enum Timeouts
    {
        /// <summary>
        /// Timeouts are disabled. No delayed delivery feature can be used.
        /// </summary>
        Disabled,
        /// <summary>
        /// Native timeouts are enabled. Delayed delivery features can be used but are at risk of message loss. Refer to the documentation for further details.
        /// </summary>
        UnsafeEnabled
    }
}