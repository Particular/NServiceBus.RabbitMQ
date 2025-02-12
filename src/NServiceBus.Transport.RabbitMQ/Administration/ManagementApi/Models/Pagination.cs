#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;

using System.Collections.Generic;
using System.Text.Json.Serialization;
class Pagination
{
    [JsonRequired]
    [JsonPropertyName("items")]
    public required List<Queue> Items { get; set; }

    [JsonRequired]
    [JsonPropertyName("page")]
    public required int Page { get; set; }

    [JsonRequired]
    [JsonPropertyName("page_count")]
    public required int PageCount { get; set; }

    [JsonRequired]
    [JsonPropertyName("page_size")]
    public required int PageSize { get; set; }
}

