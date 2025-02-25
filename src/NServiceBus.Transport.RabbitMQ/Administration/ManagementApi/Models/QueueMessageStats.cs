#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;

using System.Text.Json.Serialization;

// For ServiceControl licensing component
class QueueMessageStats
{
    [JsonPropertyName("ack")]
    public long? Ack { get; set; }
}
