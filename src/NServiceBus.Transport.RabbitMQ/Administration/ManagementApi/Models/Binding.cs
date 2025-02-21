#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;

using System.Text.Json.Serialization;

class Binding
{
    [JsonRequired]
    [JsonPropertyName("source")]
    public required string Source { get; set; }

    [JsonRequired]
    [JsonPropertyName("vhost")]
    public required string Vhost { get; set; }

    [JsonRequired]
    [JsonPropertyName("destination")]
    public required string Destination { get; set; }

    [JsonRequired]
    [JsonPropertyName("destination_type")]
    public required string DestinationType { get; set; }

    [JsonRequired]
    [JsonPropertyName("routing_key")]
    public required string RoutingKey { get; set; }

    [JsonRequired]
    [JsonPropertyName("properties_key")]
    public required string PropertiesKey { get; set; }
}

