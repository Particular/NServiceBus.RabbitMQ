#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementClient;

static class FeatureFlags
{
    /// <summary>
    /// Add a detailed queues HTTP API endpoint. Reduce number of metrics in the default endpoint.
    /// </summary>
    public const string DetailedQueuesEndpoints = "detailed_queues_endpoint";

    /// <summary>
    /// New <see href="https://www.rabbitmq.com/docs/next/metadata-store">Raft-based metadata store</see>. Fully supported as of RabbitMQ 4.0
    /// </summary>
    public const string KhepriDatabase = "khepri_db";

    /// <summary>
    /// Support queues of type `<see href="https://www.rabbitmq.com/quorum-queues.html">quorum</see>`
    /// </summary>
    public const string QuorumQueue = "quorum_queue";

    /// <summary>
    /// Support for stream filtering.
    /// </summary>
    public const string StreamFiltering = "stream_filtering";

    /// <summary>
    /// Support queues of type `<see href="https://www.rabbitmq.com/stream.html">stream</see>`.
    /// </summary>
    public const string StreamQueue = "stream_queue";
}

