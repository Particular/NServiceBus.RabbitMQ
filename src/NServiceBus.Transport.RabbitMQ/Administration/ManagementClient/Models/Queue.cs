﻿#nullable enable

namespace NServiceBus.Transport.RabbitMQ.Administration.ManagementClient.Models;

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using NServiceBus.Transport.RabbitMQ.Administration.ManagementClient.Converters;

class Queue
{
    [JsonRequired]
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("arguments")]
    public required QueueArguments Arguments { get; set; }

    [JsonPropertyName("delivery_limit")]
    [JsonConverter(typeof(DeliveryLimitConverter))]
    public int DeliveryLimit { get; set; }

    [JsonRequired]
    [JsonPropertyName("effective_policy_definition")]
    public PolicyDefinition? EffectivePolicyDefinition { get; set; }

    [JsonRequired]
    [JsonPropertyName("policy")]
    public string? AppliedPolicyName { get; set; }

    [JsonRequired]
    [JsonPropertyName("operator_policy")]
    public string? AppliedOperatorPolicyName { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> ExtraProperties { get; } = new Dictionary<string, JsonElement>();
}

