#nullable enable

namespace NServiceBus.Transport.RabbitMQ.Administration.ManagementClient.Models;

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using NServiceBus.Transport.RabbitMQ.Administration.ManagementClient.Converters;

class QueueArguments
{
    [JsonPropertyName("x-queue-type")]
    [JsonConverter(typeof(QueueTypeConverter))]
    public QueueType? QueueType { get; set; }

    [JsonPropertyName("x-delivery-limit")]
    [JsonConverter(typeof(DeliveryLimitConverter))]
    public int? DeliveryLimit { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> ExtraProperties { get; } = new Dictionary<string, JsonElement>();
}

