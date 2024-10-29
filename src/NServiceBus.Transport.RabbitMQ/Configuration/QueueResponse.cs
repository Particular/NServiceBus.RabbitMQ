namespace NServiceBus
{
    using System.Text.Json.Serialization;

    public class QueueResponse
    {
        [JsonPropertyName("delivery_limit")]
        public int DeliveryLimit { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }
}
