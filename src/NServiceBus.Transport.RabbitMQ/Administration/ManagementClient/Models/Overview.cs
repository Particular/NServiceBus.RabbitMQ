#nullable enable

namespace NServiceBus.Transport.RabbitMQ.Administration.ManagementClient.Models;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using NServiceBus.Transport.RabbitMQ.Administration.ManagementClient.Converters;

class Overview
{
    [JsonRequired]
    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonRequired]
    [JsonConverter(typeof(VersionConverter))]
    [JsonPropertyName("management_version")]
    public Version? ManagementVersion { get; set; }

    [JsonRequired]
    [JsonConverter(typeof(VersionConverter))]
    [JsonPropertyName("product_version")]
    public Version? ProductVersion { get; set; }

    [JsonRequired]
    [JsonConverter(typeof(VersionConverter))]
    [JsonPropertyName("rabbitmq_version")]
    public Version? RabbitMqVersion { get; set; }

    [JsonRequired]
    [JsonPropertyName("cluster_name")]
    public string? ClusterName { get; set; }

    [JsonRequired]
    [JsonPropertyName("node")]
    public string? Node { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> ExtraProperties { get; } = new Dictionary<string, JsonElement>();
}

