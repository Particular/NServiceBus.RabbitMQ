#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Versioning;

class SemanticVersionConverter : JsonConverter<SemanticVersion>
{
    public override SemanticVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString() ?? throw new JsonException("Missing version value");

        if (SemanticVersion.TryParse(value, out var version))
        {
            return version;
        }

        throw new JsonException($"Invalid version value {value}");
    }

    public override void Write(Utf8JsonWriter writer, SemanticVersion version, JsonSerializerOptions options) => writer.WriteStringValue(version.ToString());
}
