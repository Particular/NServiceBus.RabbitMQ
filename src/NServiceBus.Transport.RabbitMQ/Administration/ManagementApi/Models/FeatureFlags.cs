#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;

static class FeatureFlags
{
    public const string DetailedQueuesEndpoints = "detailed_queues_endpoint";

    public const string KhepriDatabase = "khepri_db";

    public const string QuorumQueue = "quorum_queue";

    public const string StreamFiltering = "stream_filtering";

    public const string StreamQueue = "stream_queue";
}

