#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;
using System.Text.Json.Serialization;

class FeatureFlag()
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("state")]
    [JsonConverter(typeof(FeatureFlagEnabledConverter))]
    public bool Enabled { get; set; }
}
