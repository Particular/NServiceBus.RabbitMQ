#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;

using System.Text.Json.Serialization;

class QueueMessageStats
{
    [JsonPropertyName("ack")]
    public long? Ack { get; set; }
}

