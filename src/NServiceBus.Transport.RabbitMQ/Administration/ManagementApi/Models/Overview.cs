#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;

using System.Text.Json.Serialization;

class Overview
{
    [JsonPropertyName("lavinmq_version")]
    public string? LavinMQBrokerVersion { get; set; }

    [JsonPropertyName("rabbitmq_version")]
    public string? RabbitMQBrokerVersion { get; set; }

    public string? BrokerVersion => RabbitMQBrokerVersion ?? LavinMQBrokerVersion;

    //NOTE the value of this is not used anywhere in the tests. ServiceControl checks the value as part of the Licensing Component however it deals with nulls.
    // For ServiceControl licensing component    
    //[JsonPropertyName("disable_stats")]
    //public required bool DisableStats { get; set; }
}
