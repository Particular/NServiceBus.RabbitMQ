namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using global::RabbitMQ.Client;
    using NServiceBus.DeliveryConstraints;
    using NServiceBus.Performance.TimeToBeReceived;

    static class RabbitMqTransportMessageExtensions
    {
        static bool TryGet<T>(IEnumerable<DeliveryConstraint> list, out T constraint) where T : DeliveryConstraint
        {
            constraint = list.OfType<T>().FirstOrDefault();

            return constraint != null;
        }

        public static void FillRabbitMqProperties(OutgoingMessage message, DispatchOptions options, IBasicProperties properties)
        {
            properties.MessageId = message.MessageId;

            if (message.Headers.ContainsKey(Headers.CorrelationId))
            {
                properties.CorrelationId = message.Headers[Headers.CorrelationId];
            }

            DiscardIfNotReceivedBefore timeToBeReceived;

            if (TryGet(options.DeliveryConstraints, out timeToBeReceived) && timeToBeReceived.MaxTime < TimeSpan.MaxValue)
            {
                properties.Expiration = timeToBeReceived.MaxTime.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
            }

            properties.Persistent = !options.DeliveryConstraints.Any(c => c is NonDurableDelivery);

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

            if (message.Headers.ContainsKey(Headers.ReplyToAddress))
            {
                properties.ReplyTo = message.Headers[Headers.ReplyToAddress];
            }
        }
    }
}