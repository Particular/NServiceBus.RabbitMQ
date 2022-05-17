namespace NServiceBus
{
    /// <summary>
    /// Specifies what queues should be used by the endpoint.
    /// </summary>
    public enum QueueType
    {
        /// <summary>
        /// Classic RabbitMQ queues. These are the standard queues and used outside cluster configuration.
        /// </summary>
        Classic,
        /// <summary>
        /// Quorum queues provide additional safety and availability in RabbitMQ clusters.
        /// </summary>
        Quorum
    }
}