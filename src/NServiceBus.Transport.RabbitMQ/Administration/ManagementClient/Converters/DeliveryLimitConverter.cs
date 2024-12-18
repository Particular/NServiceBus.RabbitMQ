#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementClient;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

class DeliveryLimitConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt32();
        }
        else if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            if (string.Equals(value, "unlimited", StringComparison.OrdinalIgnoreCase))
            {
                return -1;
            }

            throw new JsonException($"Unexpected string value for `delivery-limit` - {value}");
        }
        else
        {
            throw new JsonException($"Expected `delivery-limit` to be either a Number or the String `unlimited`, not a {reader.TokenType}");
        }
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) => writer.WriteNumberValue(value);
}
