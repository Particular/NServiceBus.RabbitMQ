#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;

using System.Text.Json.Serialization;

class Overview()
{
    [JsonPropertyName("rabbitmq_version")]
    public required string BrokerVersion { get; set; }

    [JsonPropertyName("cluster_name")]
    public required string ClusterName { get; set; }

    [JsonPropertyName("node")]
    public required string Node { get; set; }

    [JsonPropertyName("disable_stats")]
    public required bool DisableStats { get; set; }
}
