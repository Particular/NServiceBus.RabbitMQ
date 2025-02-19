#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

class PolicyTargetConverter : JsonConverter<PolicyTarget>
{
    public override PolicyTarget Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();

        return value switch
        {
            "all" => PolicyTarget.All,
            "queues" => PolicyTarget.Queues,
            "classic_queues" => PolicyTarget.ClassicQueues,
            "quorum_queues" => PolicyTarget.QuorumQueues,
            "streams" => PolicyTarget.Streams,
            "exchanges" => PolicyTarget.Exchanges,
            _ => throw new JsonException($"Unknown PolicyTarget: {value}")
        };
    }

    public override void Write(Utf8JsonWriter writer, PolicyTarget policyTarget, JsonSerializerOptions options)
    {
        var value = policyTarget switch
        {
            PolicyTarget.All => "all",
            PolicyTarget.Queues => "queues",
            PolicyTarget.ClassicQueues => "classic_queues",
            PolicyTarget.QuorumQueues => "quorum_queues",
            PolicyTarget.Streams => "streams",
            PolicyTarget.Exchanges => "exchanges",
            _ => throw new ArgumentOutOfRangeException(nameof(policyTarget), $"PolicyTarget value out of range: {policyTarget}")
        };

        writer.WriteStringValue(value);
    }
}
