#nullable enable

namespace NServiceBus.Transport.RabbitMQ.Administration.ManagementClient.Models;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using NServiceBus.Transport.RabbitMQ.Administration.ManagementClient.Converters;

class Overview
{
    [JsonPropertyName("product_name")]
    public required string ProductName { get; set; }

    [JsonConverter(typeof(VersionConverter))]
    [JsonPropertyName("management_version")]
    public required Version ManagementVersion { get; set; }

    [JsonConverter(typeof(VersionConverter))]
    [JsonPropertyName("product_version")]
    public required Version ProductVersion { get; set; }

    [JsonConverter(typeof(VersionConverter))]
    [JsonPropertyName("rabbitmq_version")]
    public required Version RabbitMqVersion { get; set; }

    [JsonPropertyName("cluster_name")]
    public required string ClusterName { get; set; }

    [JsonPropertyName("node")]
    public required string Node { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> ExtraProperties { get; } = new Dictionary<string, JsonElement>();
}

