namespace NServiceBus
{
    /// <summary>
    /// The types of queues supported by the transport. Quorum queues should be considered the default option for a replicated queue type.
    /// </summary>
    public enum QueueType
    {
        /// <summary>
        /// The original type of queue provided by RabbitMQ. They should only be used when a non-replicated queue is desired.
        /// </summary>
        Classic,

        /// <summary>
        /// A queue that provides high availability via replication and focuses on data safety under network partition and failure scenarios.
        /// </summary>
        Quorum
    }
}