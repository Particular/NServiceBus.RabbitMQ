#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;
using System.Text.Json.Serialization;

class Policy()
{
    [JsonPropertyName("pattern")]
    public required string Pattern { get; set; }

    [JsonPropertyName("apply-to")]
    [JsonConverter(typeof(PolicyTargetConverter))]
    public required PolicyTarget ApplyTo { get; set; }

    [JsonPropertyName("definition")]
    public required PolicyDefinition Definition { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; }
}
