#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementClient;

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

// This is to prevent Fody throwing an error on classes with `required` properties (since the compiler marks the default constructor with an `[Obsolete]` attribute)
// https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-11.0/required-members#metadata-representation
[method: DoNotWarnAboutObsoleteUsage]
class Policy()
{
    [JsonPropertyName("vhost")]
    public string VirtualHost { get; set; } = "/";

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("pattern")]
    public required string Pattern { get; set; }

    [JsonConverter(typeof(PolicyTargetConverter))]
    [JsonPropertyName("apply-to")]
    public required PolicyTarget ApplyTo { get; set; }

    [JsonPropertyName("definition")]
    public required PolicyDefinition Definition { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> ExtraProperties { get; } = new Dictionary<string, JsonElement>();
}

