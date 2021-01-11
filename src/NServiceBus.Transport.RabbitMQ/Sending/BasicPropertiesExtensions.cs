namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using global::RabbitMQ.Client;

    static class BasicPropertiesExtensions
    {
        public static void Fill(this IBasicProperties properties, OutgoingMessage message, DispatchProperties dispatchProperties)
        {
            if (message.MessageId != null)
            {
                properties.MessageId = message.MessageId;
            }

            var messageHeaders = message.Headers ?? new Dictionary<string, string>();

            var delayed = CalculateDelay(dispatchProperties, out var delay);

            properties.Persistent = !messageHeaders.Remove(UseNonPersistentDeliveryHeader);

            properties.Headers = messageHeaders.ToDictionary(p => p.Key, p => (object)p.Value);

            if (delayed)
            {
                properties.Headers[DelayInfrastructure.DelayHeader] = Convert.ToInt32(delay);
            }

            if (dispatchProperties.DiscardIfNotReceivedBefore != null && dispatchProperties.DiscardIfNotReceivedBefore.MaxTime < TimeSpan.MaxValue)
            {
                // align with TimeoutManager behavior
                if (delayed)
                {
                    throw new Exception("Postponed delivery of messages with TimeToBeReceived set is not supported. Remove the TimeToBeReceived attribute to postpone messages of this type.");
                }

                properties.Expiration = dispatchProperties.DiscardIfNotReceivedBefore.MaxTime.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
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

        static bool CalculateDelay(DispatchProperties dispatchProperties, out long delay)
        {
            delay = 0;
            var delayed = false;

            if (dispatchProperties.DoNotDeliverBefore != null)
            {
                delayed = true;
                delay = Convert.ToInt64(Math.Ceiling((dispatchProperties.DoNotDeliverBefore.At - DateTimeOffset.UtcNow).TotalSeconds));

                if (delay > DelayInfrastructure.MaxDelayInSeconds)
                {
                    throw new Exception($"Message cannot set to be delivered at '{dispatchProperties.DoNotDeliverBefore.At}' because the delay exceeds the maximum delay value '{TimeSpan.FromSeconds(DelayInfrastructure.MaxDelayInSeconds)}'.");
                }

            }
            else if (dispatchProperties.DelayDeliveryWith != null)
            {
                delayed = true;
                delay = Convert.ToInt64(Math.Ceiling(dispatchProperties.DelayDeliveryWith.Delay.TotalSeconds));

                if (delay > DelayInfrastructure.MaxDelayInSeconds)
                {
                    throw new Exception($"Message cannot be delayed by '{dispatchProperties.DelayDeliveryWith.Delay}' because it exceeds the maximum delay value '{TimeSpan.FromSeconds(DelayInfrastructure.MaxDelayInSeconds)}'.");
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

        public const string ConfirmationIdHeader = "NServiceBus.Transport.RabbitMQ.ConfirmationId";
        public const string UseNonPersistentDeliveryHeader = "NServiceBus.Transport.RabbitMQ.UseNonPersistentDelivery";
    }
}
