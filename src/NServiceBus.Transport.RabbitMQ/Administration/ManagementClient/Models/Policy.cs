#nullable enable

namespace NServiceBus.Transport.RabbitMQ.Administration.ManagementClient.Models;

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using NServiceBus.Transport.RabbitMQ.Administration.ManagementClient.Converters;

class Policy
{
    [JsonRequired]
    [JsonPropertyName("vhost")]
    public string VirtualHost { get; set; } = "/";

    [JsonRequired]
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonRequired]
    [JsonPropertyName("pattern")]
    public required string Pattern { get; set; }

    [JsonRequired]
    [JsonConverter(typeof(PolicyTargetConverter))]
    [JsonPropertyName("apply-to")]
    public PolicyTarget? ApplyTo { get; set; }

    [JsonRequired]
    [JsonPropertyName("definition")]
    public PolicyDefinition? Definition { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> ExtraProperties { get; } = new Dictionary<string, JsonElement>();
}

