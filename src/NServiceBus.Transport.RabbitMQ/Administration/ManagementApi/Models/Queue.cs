#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;

using System.Text.Json.Serialization;

class Queue()
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("type")]
    [JsonConverter(typeof(QueueTypeConverter))]
    public QueueType? QueueType { get; set; }

    [JsonPropertyName("arguments")]
    public required QueueArguments Arguments { get; set; }

    [JsonPropertyName("delivery_limit")]
    [JsonConverter(typeof(DeliveryLimitConverter))]
    public int? DeliveryLimit { get; set; }

    [JsonPropertyName("effective_policy_definition")]
    public PolicyDefinition? EffectivePolicyDefinition { get; set; }

    [JsonPropertyName("policy")]
    public string? AppliedPolicyName { get; set; }

    [JsonPropertyName("operator_policy")]
    public string? AppliedOperatorPolicyName { get; set; }

    [JsonPropertyName("message_stats")]
    public QueueMessageStats? MessageStats { get; set; }

    [JsonPropertyName("vhost")]
    public required string Vhost { get; set; }

    public int GetDeliveryLimit()
    {
        // RabbitMQ 4.x
        if (DeliveryLimit is not null)
        {
            return DeliveryLimit.Value;
        }

        // RabbitMQ 3.x
        // The broker doesn't tell us what the actual delivery limit is, so we have to figure it out
        // We have to find the lowest value from the possible places in can be configured

        int? limit = null;

        if (EffectivePolicyDefinition?.DeliveryLimit is not null)
        {
            limit = EffectivePolicyDefinition.DeliveryLimit;
        }

        if (Arguments.DeliveryLimit is not null)
        {
            if (limit is null || (limit is not null && Arguments.DeliveryLimit < limit))
            {
                limit = Arguments.DeliveryLimit;
            }
        }

        // queue argument can be negative but is still treated as 0 on 3.x
        if (limit is not null and < 0)
        {
            limit = 0;
        }

        return limit ?? -1;
    }
}
