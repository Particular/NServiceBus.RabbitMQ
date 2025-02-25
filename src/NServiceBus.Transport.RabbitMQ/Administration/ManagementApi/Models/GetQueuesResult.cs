#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;

using System.Collections.Generic;
using System.Text.Json.Serialization;

// For ServiceControl licensing component
class GetQueuesResult
{
    [JsonPropertyName("items")]
    public required List<Queue> Items { get; set; }

    [JsonPropertyName("page")]
    public required int Page { get; set; }

    [JsonPropertyName("page_count")]
    public required int PageCount { get; set; }
}
