namespace NServiceBus
{
    /// <summary>
    /// Specifies what queues should be used by the endpoint.
    /// </summary>
    public enum QueueMode
    {
        /// <summary>
        /// Classic RabbitMQ queues. These are the standard queues and used outside cluster configuration.
        /// </summary>
        Classic,
        /// <summary>
        /// Quorum queues provide additional safety and availability in RabbitMQ clusters. Quorum mode does not support delayed messages.
        /// </summary>
        Quorum,
        /// <summary>
        /// Quorum queues but with the delayed delivery infrastructure (using classic queues) enabled.
        /// </summary>
        QuorumWithUnsafeTimeouts
    }
}