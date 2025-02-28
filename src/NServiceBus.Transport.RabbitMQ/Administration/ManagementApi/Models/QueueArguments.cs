#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;
using System.Text.Json.Serialization;

class QueueArguments
{
    [JsonPropertyName("x-delivery-limit")]
    [JsonConverter(typeof(DeliveryLimitConverter))]
    public int? DeliveryLimit { get; set; }

    [JsonPropertyName("x-queue-type")]
    [JsonConverter(typeof(QueueTypeConverter))]
    public QueueType? QueueType { get; set; }
}
