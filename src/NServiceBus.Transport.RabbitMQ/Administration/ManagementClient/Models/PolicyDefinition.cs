#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementClient;

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

class PolicyDefinition
{
    [JsonPropertyName("delivery-limit")]
    [JsonConverter(typeof(DeliveryLimitConverter))]
    public int? DeliveryLimit { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> ExtraProperties { get; } = new Dictionary<string, JsonElement>();
}

