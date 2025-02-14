#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;

using System.Text.Json.Serialization;
using NuGet.Versioning;

class Overview()
{
    [JsonPropertyName("rabbitmq_version")]
    [JsonConverter(typeof(SemanticVersionConverter))]
    public required SemanticVersion BrokerVersion { get; set; }
}

