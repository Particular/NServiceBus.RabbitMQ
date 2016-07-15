﻿namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using global::RabbitMQ.Client;
    using DeliveryConstraints;
    using Performance.TimeToBeReceived;
    using Transports;

    static class BasicPropertiesExtensions
    {
        public static void Fill(this IBasicProperties properties, OutgoingMessage message, IEnumerable<DeliveryConstraint> deliveryConstraints)
        {
            properties.MessageId = message.MessageId;

            if (message.Headers.ContainsKey(NServiceBus.Headers.CorrelationId))
            {
                properties.CorrelationId = message.Headers[NServiceBus.Headers.CorrelationId];
            }

            DiscardIfNotReceivedBefore timeToBeReceived;

            if (TryGet(deliveryConstraints, out timeToBeReceived) && timeToBeReceived.MaxTime < TimeSpan.MaxValue)
            {
                properties.Expiration = timeToBeReceived.MaxTime.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
            }

            properties.Persistent = !deliveryConstraints.Any(c => c is NonDurableDelivery);

            properties.Headers = message.Headers.ToDictionary(p => p.Key, p => (object)p.Value);

            string messageTypesHeader;
            if (message.Headers.TryGetValue(NServiceBus.Headers.EnclosedMessageTypes, out messageTypesHeader))
            {
                var index = messageTypesHeader.IndexOf(',');

                if (index > -1)
                {
                    properties.Type = messageTypesHeader.Substring(0, index);
                }
                else
                {
                    properties.Type = messageTypesHeader;
                }
            }

            if (message.Headers.ContainsKey(NServiceBus.Headers.ContentType))
            {
                properties.ContentType = message.Headers[NServiceBus.Headers.ContentType];
            }
            else
            {
                properties.ContentType = "application/octet-stream";
            }

            if (message.Headers.ContainsKey(NServiceBus.Headers.ReplyToAddress))
            {
                properties.ReplyTo = message.Headers[NServiceBus.Headers.ReplyToAddress];
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

        static bool TryGet<T>(IEnumerable<DeliveryConstraint> list, out T constraint) where T : DeliveryConstraint
        {
            constraint = list.OfType<T>().FirstOrDefault();

            return constraint != null;
        }

        const string confirmationIdHeader = "NServiceBus.Transport.RabbitMQ.ConfirmationId";
    }
}
