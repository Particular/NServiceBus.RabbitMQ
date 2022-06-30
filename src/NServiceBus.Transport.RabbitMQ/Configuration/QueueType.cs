namespace NServiceBus
{
    /// <summary>
    /// Specifies what queues should be used by the endpoint. <see cref="Quorum" /> queues should be considered the default option for a replicated queue type.
    /// </summary>
    public enum QueueType
    {
        /// <summary>
        /// The original classic/mirrored RabbitMQ queues. These are provided for backward compatibility, but have weaknesses
        /// compared to <see cref="Quroum" /> queues which make it less than ideal for replicated queues where reliability is key. 
        /// </summary>
        Classic,
        /// <summary>
        /// Quorum queues provide additional safety and availability in RabbitMQ clusters. Should be considered the default option for a replicated queue type.
        /// </summary>
        Quorum
    }
}