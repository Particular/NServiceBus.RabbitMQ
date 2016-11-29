namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using DeliveryConstraints;
    using global::RabbitMQ.Client;
    using Performance.TimeToBeReceived;

    static class BasicPropertiesExtensions
    {
        public static void Fill(this IBasicProperties properties, OutgoingMessage message, List<DeliveryConstraint> deliveryConstraints)
        {
            if (message.MessageId != null)
            {
                properties.MessageId = message.MessageId;
            }

            DiscardIfNotReceivedBefore timeToBeReceived;
            if (TryGet(deliveryConstraints, out timeToBeReceived) && timeToBeReceived.MaxTime < TimeSpan.MaxValue)
            {
                properties.Expiration = timeToBeReceived.MaxTime.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
            }

            properties.Persistent = !deliveryConstraints.Any(c => c is NonDurableDelivery);

            if (message.Headers == null)
            {
                return;
            }

            properties.Headers = message.Headers.ToDictionary(p => p.Key, p => (object)p.Value);

            string correlationId;
            if (message.Headers.TryGetValue(NServiceBus.Headers.CorrelationId, out correlationId) && correlationId != null)
            {
                properties.CorrelationId = correlationId;
            }

            string enclosedMessageTypes;
            if (message.Headers.TryGetValue(NServiceBus.Headers.EnclosedMessageTypes, out enclosedMessageTypes) && enclosedMessageTypes != null)
            {
                var index = enclosedMessageTypes.IndexOf(',');

                if (index > -1)
                {
                    properties.Type = enclosedMessageTypes.Substring(0, index);
                }
                else
                {
                    properties.Type = enclosedMessageTypes;
                }
            }

            string contentType;
            if (message.Headers.TryGetValue(NServiceBus.Headers.ContentType, out contentType) && contentType != null)
            {
                properties.ContentType = contentType;
            }
            else
            {
                properties.ContentType = "application/octet-stream";
            }

            string replyToAddress;
            if (message.Headers.TryGetValue(NServiceBus.Headers.ReplyToAddress, out replyToAddress) && replyToAddress != null)
            {
                properties.ReplyTo = replyToAddress;
            }
        }

        public static void SetConfirmationId(this IBasicProperties properties, ulong confirmationId)
        {
            properties.Headers[confirmationIdHeader] = confirmationId.ToString();
        }

        public static bool TryGetConfirmationId(this IBasicProperties properties, out ulong confirmationId)
        {
            confirmationId = 0;

            if (properties.Headers.ContainsKey(confirmationIdHeader))
            {
                var headerBytes = properties.Headers[confirmationIdHeader] as byte[];
                var headerString = Encoding.UTF8.GetString(headerBytes ?? new byte[0]);

                return UInt64.TryParse(headerString, out confirmationId);
            }

            return false;
        }

        static bool TryGet<T>(List<DeliveryConstraint> list, out T constraint) where T : DeliveryConstraint
        {
            constraint = list.OfType<T>().FirstOrDefault();

            return constraint != null;
        }

        const string confirmationIdHeader = "NServiceBus.Transport.RabbitMQ.ConfirmationId";
    }
}
