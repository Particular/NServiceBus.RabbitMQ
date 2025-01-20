#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementClient;

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

class QueueArguments
{
    [JsonPropertyName("x-queue-type")]
    [JsonConverter(typeof(QueueTypeConverter))]
    public QueueType? QueueType { get; set; }

    [JsonPropertyName("x-delivery-limit")]
    [JsonConverter(typeof(DeliveryLimitConverter))]
    public int? DeliveryLimit { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> ExtraProperties { get; init; } = new Dictionary<string, JsonElement>();
}

