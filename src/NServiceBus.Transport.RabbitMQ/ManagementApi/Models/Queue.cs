#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi.Models;

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using NServiceBus.Transport.RabbitMQ.ManagementApi.Converters;

class Queue
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("arguments")]
    public required QueueArguments Arguments { get; set; }

    [JsonPropertyName("delivery_limit")]
    [JsonConverter(typeof(DeliveryLimitConverter))]
    public int DeliveryLimit { get; set; }

    [JsonPropertyName("effective_policy_definition")]
    public required PolicyDefinition EffectivePolicyDefinition { get; set; }

    [JsonPropertyName("policy")]
    public string? AppliedPolicyName { get; set; }

    [JsonPropertyName("operator_policy")]
    public string? AppliedOperatorPolicyName { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> ExtraProperties { get; } = new Dictionary<string, JsonElement>();
}

