namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using global::RabbitMQ.Client.Events;
    class MessageConverter
    {
        public MessageConverter(Func<BasicDeliverEventArgs, string> messageIdStrategy)
        {
            this.messageIdStrategy = messageIdStrategy;
        }

        public string RetrieveMessageId(BasicDeliverEventArgs message, Dictionary<string, string> headers)
        {
            var messageId = messageIdStrategy(message);

            if (string.IsNullOrWhiteSpace(messageId) && !headers.TryGetValue(Headers.MessageId, out messageId))
            {
                throw new InvalidOperationException("The message ID strategy did not provide a message ID, and the message does not have an 'NServiceBus.MessageId' header.");
            }

            return messageId;
        }

        public Dictionary<string, string> RetrieveHeaders(BasicDeliverEventArgs message)
        {
            var properties = message.BasicProperties;
            var messageHeaders = properties.Headers;

            if (messageHeaders != null)
            {
                //These headers need to be removed so that they won't be copied to an outgoing message if this message gets forwarded
                messageHeaders.Remove(DelayInfrastructure.DelayHeader);
                messageHeaders.Remove(DelayInfrastructure.XDeathHeader);
                messageHeaders.Remove(DelayInfrastructure.XFirstDeathExchangeHeader);
                messageHeaders.Remove(DelayInfrastructure.XFirstDeathQueueHeader);
                messageHeaders.Remove(DelayInfrastructure.XFirstDeathReasonHeader);
                messageHeaders.Remove(BasicPropertiesExtensions.ConfirmationIdHeader);
            }

            // Leaving space for ReplyTo, CorrelationId, DeliveryMode, EnclosedMessageTypes conditionally
            // added below. This is a bit cumbersome and need to be changed when things are conditionally added below
            // but it prevents the header dictionary from growing and relocating which creates quite a bit of
            // memory allocations and eats up CPU cycles.
            const int extraCapacity = 4;
            var deserializedHeaders = DeserializeHeaders(messageHeaders, extraCapacity);

            if (properties.IsReplyToPresent())
            {
                deserializedHeaders[Headers.ReplyToAddress] = properties.ReplyTo;
            }

            if (deserializedHeaders.TryGetValue("NServiceBus.RabbitMQ.CallbackQueue", out var callbackQueue))
            {
                deserializedHeaders[Headers.ReplyToAddress] = callbackQueue;
            }

            if (properties.IsCorrelationIdPresent())
            {
                deserializedHeaders[Headers.CorrelationId] = properties.CorrelationId;
            }

            if (properties.IsDeliveryModePresent() && properties.DeliveryMode == 1)
            {
                deserializedHeaders[BasicPropertiesExtensions.UseNonPersistentDeliveryHeader] = bool.TrueString;
            }

            //When doing native interop we only require the type to be set the "fullName" of the message
            if (!deserializedHeaders.ContainsKey(Headers.EnclosedMessageTypes) && properties.IsTypePresent())
            {
                deserializedHeaders[Headers.EnclosedMessageTypes] = properties.Type;
            }

            //These headers need to be removed so that they won't be copied to an outgoing message if this message gets forwarded
            //They can't be removed before deserialization because the value is used by the message pump
            deserializedHeaders.Remove("x-delivery-count");

            return deserializedHeaders;
        }

        public static string DefaultMessageIdStrategy(BasicDeliverEventArgs message)
        {
            var properties = message.BasicProperties;

            if (!properties.IsMessageIdPresent() || string.IsNullOrWhiteSpace(properties.MessageId))
            {
                throw new InvalidOperationException("A non-empty 'message-id' property is required when running NServiceBus on top of RabbitMQ. If this is an interop message, then set the 'message-id' property before publishing the message.");
            }

            return properties.MessageId;
        }

        static Dictionary<string, string> DeserializeHeaders(IDictionary<string, object> headers, int extraCapacity)
        {
            if (headers is null)
            {
                return new Dictionary<string, string>(extraCapacity);
            }

            var deserializedHeaders = new Dictionary<string, string>(headers.Count + extraCapacity);
            foreach (var header in headers)
            {
                deserializedHeaders.Add(header.Key, ValueToString(header.Value));
            }
            return deserializedHeaders;
        }

        static string ValueToString(object value)
        {
            if (value is byte[] bytes)
            {
                return Encoding.UTF8.GetString(bytes.AsSpan());
            }

            if (value is Dictionary<string, object> dictionary)
            {
                var sb = new StringBuilder();

                foreach (var kvp in dictionary)
                {
                    sb.Append(kvp.Key);
                    sb.Append('=');
                    sb.Append(ValueToString(kvp.Value));
                    sb.Append(',');
                }

                if (sb.Length > 0)
                {
                    sb.Remove(sb.Length - 1, 1);
                }

                return sb.ToString();
            }

            if (value is List<object> list)
            {
                var sb = new StringBuilder();

                foreach (var entry in list)
                {
                    sb.Append(ValueToString(entry));
                    sb.Append(';');
                }

                if (sb.Length > 0)
                {
                    sb.Remove(sb.Length - 1, 1);
                }

                return sb.ToString();
            }

            if (value is global::RabbitMQ.Client.AmqpTimestamp timestamp)
            {
                return DateTimeOffsetHelper.ToWireFormattedString(UnixEpoch.AddSeconds(timestamp.UnixTime));
            }

            return value?.ToString();
        }

        readonly Func<BasicDeliverEventArgs, string> messageIdStrategy;

        static readonly DateTimeOffset UnixEpoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }
}
