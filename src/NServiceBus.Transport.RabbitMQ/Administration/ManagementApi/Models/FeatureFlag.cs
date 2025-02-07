#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

class FeatureFlag()
{
    [JsonRequired]
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonConverter(typeof(FeatureFlagEnabledConverter))]
    [JsonPropertyName("state")]
    public bool IsEnabled { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> ExtraProperties { get; } = new Dictionary<string, JsonElement>();
}

