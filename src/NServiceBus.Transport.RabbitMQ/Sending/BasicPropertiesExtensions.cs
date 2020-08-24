namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using DelayedDelivery;
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

            properties.Persistent = !deliveryConstraints.Any(c => c is NonDurableDelivery);

            var messageHeaders = message.Headers ?? new Dictionary<string, string>();

            var delayed = CalculateDelay(deliveryConstraints, out var delay);

            properties.Headers = messageHeaders.ToDictionary(p => p.Key, p => (object)p.Value);

            if (delayed)
            {
                properties.Headers[DelayInfrastructure.DelayHeader] = Convert.ToInt32(delay);
            }

            if (deliveryConstraints.TryGet(out DiscardIfNotReceivedBefore timeToBeReceived) && timeToBeReceived.MaxTime < TimeSpan.MaxValue)
            {
                // align with TimeoutManager behavior
                if (delayed)
                {
                    throw new Exception("Postponed delivery of messages with TimeToBeReceived set is not supported. Remove the TimeToBeReceived attribute to postpone messages of this type.");
                }

                properties.Expiration = timeToBeReceived.MaxTime.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
            }

            if (messageHeaders.TryGetValue(NServiceBus.Headers.CorrelationId, out var correlationId) && correlationId != null)
            {
                properties.CorrelationId = correlationId;
            }

            if (messageHeaders.TryGetValue(NServiceBus.Headers.EnclosedMessageTypes, out var enclosedMessageTypes) && enclosedMessageTypes != null)
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

            if (messageHeaders.TryGetValue(NServiceBus.Headers.ContentType, out var contentType) && contentType != null)
            {
                properties.ContentType = contentType;
            }
            else
            {
                properties.ContentType = "application/octet-stream";
            }

            if (messageHeaders.TryGetValue(NServiceBus.Headers.ReplyToAddress, out var replyToAddress) && replyToAddress != null)
            {
                properties.ReplyTo = replyToAddress;
            }
        }

        static bool CalculateDelay(List<DeliveryConstraint> deliveryConstraints, out long delay)
        {
            delay = 0;
            var delayed = false;

            if (deliveryConstraints.TryGet(out DoNotDeliverBefore doNotDeliverBefore))
            {
                delayed = true;
                delay = Convert.ToInt64(Math.Ceiling((doNotDeliverBefore.At - DateTime.UtcNow).TotalSeconds));

                if (delay > DelayInfrastructure.MaxDelayInSeconds)
                {
                    throw new Exception($"Message cannot be sent with {nameof(DoNotDeliverBefore)} value '{doNotDeliverBefore.At}' because it exceeds the maximum delay value '{TimeSpan.FromSeconds(DelayInfrastructure.MaxDelayInSeconds)}'.");
                }

            }
            else if (deliveryConstraints.TryGet(out DelayDeliveryWith delayDeliveryWith))
            {
                delayed = true;
                delay = Convert.ToInt64(Math.Ceiling(delayDeliveryWith.Delay.TotalSeconds));

                if (delay > DelayInfrastructure.MaxDelayInSeconds)
                {
                    throw new Exception($"Message cannot be sent with {nameof(DelayDeliveryWith)} value '{delayDeliveryWith.Delay}' because it exceeds the maximum delay value '{TimeSpan.FromSeconds(DelayInfrastructure.MaxDelayInSeconds)}'.");
                }
            }

            return delayed;
        }

        public static void SetConfirmationId(this IBasicProperties properties, ulong confirmationId)
        {
            properties.Headers[ConfirmationIdHeader] = confirmationId.ToString();
        }

        public static bool TryGetConfirmationId(this IBasicProperties properties, out ulong confirmationId)
        {
            confirmationId = 0;

            return properties.Headers.TryGetValue(ConfirmationIdHeader, out var value) &&
                ulong.TryParse(Encoding.UTF8.GetString(value as byte[] ?? Array.Empty<byte>()), out confirmationId);
        }

        static bool TryGet<T>(this List<DeliveryConstraint> list, out T constraint) where T : DeliveryConstraint =>
            (constraint = list.OfType<T>().FirstOrDefault()) != null;

        public const string ConfirmationIdHeader = "NServiceBus.Transport.RabbitMQ.ConfirmationId";
    }
}
