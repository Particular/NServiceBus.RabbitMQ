#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;

using System.Text.Json.Serialization;

class Overview()
{
    [JsonPropertyName("rabbitmq_version")]
    public required string BrokerVersion { get; set; }

    // For ServiceControl licensing component
    [JsonPropertyName("disable_stats")]
    public required bool DisableStats { get; set; }
}
