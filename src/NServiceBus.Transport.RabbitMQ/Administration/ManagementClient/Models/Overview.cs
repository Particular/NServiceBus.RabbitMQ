#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementClient;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

// This is to prevent Fody throwing an error on classes with `required` properties (since the compiler marks the default constructor with an `[Obsolete]` attribute)
// https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-11.0/required-members#metadata-representation
[method: DoNotWarnAboutObsoleteUsage]
class Overview()
{
    [JsonPropertyName("product_name")]
    public required string ProductName { get; set; }

    [JsonConverter(typeof(VersionConverter))]
    [JsonPropertyName("management_version")]
    public required Version ManagementVersion { get; set; }

    [JsonConverter(typeof(VersionConverter))]
    [JsonPropertyName("product_version")]
    public required Version ProductVersion { get; set; }

    [JsonConverter(typeof(VersionConverter))]
    [JsonPropertyName("rabbitmq_version")]
    public required Version RabbitMqVersion { get; set; }

    [JsonPropertyName("cluster_name")]
    public required string ClusterName { get; set; }

    [JsonPropertyName("node")]
    public required string Node { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> ExtraProperties { get; } = new Dictionary<string, JsonElement>();
}

