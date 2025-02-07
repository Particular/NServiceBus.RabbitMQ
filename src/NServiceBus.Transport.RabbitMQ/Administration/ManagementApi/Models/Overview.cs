#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;

using System;
using System.Text.Json.Serialization;

class Overview()
{
    [JsonPropertyName("product_name")]
    public required string ProductName { get; set; }

    [JsonPropertyName("management_version")]
    [JsonConverter(typeof(VersionConverter))]
    public required Version ManagementVersion { get; set; }

    [JsonPropertyName("product_version")]
    [JsonConverter(typeof(VersionConverter))]
    public required Version ProductVersion { get; set; }

    [JsonPropertyName("rabbitmq_version")]
    [JsonConverter(typeof(VersionConverter))]
    public required Version RabbitMqVersion { get; set; }

    [JsonPropertyName("cluster_name")]
    public required string ClusterName { get; set; }

    [JsonPropertyName("node")]
    public required string Node { get; set; }
}

