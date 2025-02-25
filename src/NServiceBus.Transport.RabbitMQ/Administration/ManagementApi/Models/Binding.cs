#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;

using System.Text.Json.Serialization;

// For ServiceControl licensing component
class Binding
{
    [JsonPropertyName("source")]
    public required string Source { get; set; }

    [JsonPropertyName("destination")]
    public required string Destination { get; set; }

    [JsonPropertyName("destination_type")]
    public required string DestinationType { get; set; }

    [JsonPropertyName("routing_key")]
    public required string RoutingKey { get; set; }

    [JsonPropertyName("properties_key")]
    public required string PropertiesKey { get; set; }
}
