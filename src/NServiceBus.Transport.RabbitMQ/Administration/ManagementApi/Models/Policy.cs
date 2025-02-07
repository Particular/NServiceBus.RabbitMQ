#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

class Policy()
{
    [JsonPropertyName("vhost")]
    public string VirtualHost { get; set; } = "/";

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("pattern")]
    public required string Pattern { get; set; }

    [JsonPropertyName("apply-to")]
    [JsonConverter(typeof(PolicyTargetConverter))]
    public required PolicyTarget ApplyTo { get; set; }

    [JsonPropertyName("definition")]
    public required PolicyDefinition Definition { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> ExtraProperties { get; init; } = [];
}

