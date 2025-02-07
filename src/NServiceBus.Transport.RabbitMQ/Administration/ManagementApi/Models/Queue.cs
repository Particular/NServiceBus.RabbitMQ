#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;
using System.Text.Json.Serialization;

class Queue()
{
    [JsonRequired]
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonRequired]
    [JsonPropertyName("arguments")]
    public required QueueArguments Arguments { get; set; }

    [JsonPropertyName("delivery_limit")]
    [JsonConverter(typeof(DeliveryLimitConverter))]
    public int DeliveryLimit { get; set; }

    [JsonPropertyName("effective_policy_definition")]
    public PolicyDefinition? EffectivePolicyDefinition { get; set; }

    [JsonPropertyName("policy")]
    public string? AppliedPolicyName { get; set; }

    [JsonPropertyName("operator_policy")]
    public string? AppliedOperatorPolicyName { get; set; }
}