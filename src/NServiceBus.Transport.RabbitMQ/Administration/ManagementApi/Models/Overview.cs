#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;

using System.Text.Json.Serialization;
using NuGet.Versioning;

class Overview()
{
    [JsonPropertyName("product_name")]
    public required string ProductName { get; set; }

    [JsonPropertyName("management_version")]
    [JsonConverter(typeof(SemanticVersionConverter))]
    public required SemanticVersion ManagementVersion { get; set; }

    [JsonPropertyName("product_version")]
    [JsonConverter(typeof(SemanticVersionConverter))]
    public required SemanticVersion ProductVersion { get; set; }

    [JsonPropertyName("rabbitmq_version")]
    [JsonConverter(typeof(SemanticVersionConverter))]
    public required SemanticVersion BrokerVersion { get; set; }

    [JsonPropertyName("cluster_name")]
    public required string ClusterName { get; set; }

    [JsonPropertyName("node")]
    public required string Node { get; set; }
}

