#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementClient;

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

// This is to prevent Fody throwing an error on classes with `required` properties (since the compiler marks the default constructor with an `[Obsolete]` attribute)
// https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-11.0/required-members#metadata-representation
[method: DoNotWarnAboutObsoleteUsage]
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

    [JsonExtensionData]
    public IDictionary<string, JsonElement> ExtraProperties { get; init; } = new Dictionary<string, JsonElement>();
}