#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;
using System.Text.Json.Serialization;

class PolicyDefinition
{
    [JsonPropertyName("delivery-limit")]
    [JsonConverter(typeof(DeliveryLimitConverter))]
    public int? DeliveryLimit { get; set; }
}

