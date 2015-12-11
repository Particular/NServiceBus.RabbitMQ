namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using System.Linq;
    using global::RabbitMQ.Client;
    using Unicast;

    static class RabbitMqTransportMessageExtensions
    {
        public static void FillRabbitMqProperties(TransportMessage message, DeliveryOptions options, IBasicProperties properties)
        {
            properties.MessageId = message.Id;

            if (!String.IsNullOrEmpty(message.CorrelationId))
            {
                properties.CorrelationId = message.CorrelationId;
            }

            if (message.TimeToBeReceived < TimeSpan.MaxValue)
            {
                properties.Expiration = message.TimeToBeReceived.TotalMilliseconds.ToString();
            }

            properties.Persistent = message.Recoverable;

            properties.Headers = message.Headers.ToDictionary(p => p.Key, p => (object)p.Value);

            if (message.Headers.ContainsKey(Headers.EnclosedMessageTypes))
            {
                properties.Type = message.Headers[Headers.EnclosedMessageTypes].Split(new[]
                {
                    ','
                }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            }

            if (message.Headers.ContainsKey(Headers.ContentType))
            {
                properties.ContentType = message.Headers[Headers.ContentType];
            }
            else
            {
                properties.ContentType = "application/octet-stream";
            }

            var replyToAddress = options.ReplyToAddress ?? message.ReplyToAddress;
            if (replyToAddress != null)
            {
                properties.ReplyTo = replyToAddress.Queue;
            }
        }
    }
}