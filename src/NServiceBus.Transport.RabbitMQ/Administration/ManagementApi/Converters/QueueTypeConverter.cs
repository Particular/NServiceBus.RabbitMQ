#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

class QueueTypeConverter : JsonConverter<QueueType>
{
    public override QueueType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();

        return value switch
        {
            "quorum" => QueueType.Quorum,
            "classic" => QueueType.Classic,
            "stream" => QueueType.Stream,
            _ => throw new JsonException($"Unknown QueueType: {value}")
        };
    }

    public override void Write(Utf8JsonWriter writer, QueueType queueType, JsonSerializerOptions options)
    {
        var value = queueType switch
        {
            QueueType.Quorum => "quorum",
            QueueType.Classic => "classic",
            QueueType.Stream => "stream",
            _ => throw new ArgumentOutOfRangeException(nameof(queueType), $"QueueType value out of range: {queueType}")
        };

        writer.WriteStringValue(value);
    }
}
