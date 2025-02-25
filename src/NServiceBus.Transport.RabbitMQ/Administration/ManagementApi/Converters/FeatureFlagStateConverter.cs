#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

class FeatureFlagEnabledConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString()?.Equals("enabled", StringComparison.OrdinalIgnoreCase) ?? false;

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value ? "enabled" : "disabled");
}
