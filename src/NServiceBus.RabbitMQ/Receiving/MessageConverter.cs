namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using global::RabbitMQ.Client.Events;
    using Logging;

    class MessageConverter
    {
        public MessageConverter()
        {
            messageIdStrategy = DefaultMessageIdStrategy;
        }

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

            var deserializedHeaders = DeserializeHeaders(messageHeaders);

            if (properties.IsReplyToPresent())
            {
                deserializedHeaders[Headers.ReplyToAddress] = properties.ReplyTo;
            }

            if (properties.IsCorrelationIdPresent())
            {
                deserializedHeaders[Headers.CorrelationId] = properties.CorrelationId;
            }

            //When doing native interop we only require the type to be set the "fullName" of the message
            if (!deserializedHeaders.ContainsKey(Headers.EnclosedMessageTypes) && properties.IsTypePresent())
            {
                deserializedHeaders[Headers.EnclosedMessageTypes] = properties.Type;
            }

            if (properties.IsDeliveryModePresent())
            {
                deserializedHeaders[Headers.NonDurableMessage] = (properties.DeliveryMode == 1).ToString();
            }

            if (deserializedHeaders.ContainsKey("NServiceBus.RabbitMQ.CallbackQueue"))
            {
                deserializedHeaders[Headers.ReplyToAddress] = deserializedHeaders["NServiceBus.RabbitMQ.CallbackQueue"];
            }

            return deserializedHeaders;
        }

        string DefaultMessageIdStrategy(BasicDeliverEventArgs message)
        {
            var properties = message.BasicProperties;

            if (!properties.IsMessageIdPresent() || string.IsNullOrWhiteSpace(properties.MessageId))
            {
                throw new InvalidOperationException("A non-empty 'message-id' property is required when running NServiceBus on top of RabbitMQ. If this is an interop message, then set the 'message-id' property before publishing the message");
            }

            return properties.MessageId;
        }

        static Dictionary<string, string> DeserializeHeaders(IDictionary<string, object> headers)
        {
            var deserializedHeaders = new Dictionary<string, string>();
            var messageHeaders = headers as Dictionary<string, object>;

            if (messageHeaders != null)
            {
                foreach (var header in messageHeaders)
                {
                    deserializedHeaders.Add(header.Key, ValueToString(header.Value));
                }
            }

            return deserializedHeaders;
        }

        static string ValueToString(object value)
        {
            var bytes = value as byte[];
            if (bytes != null)
            {
                return Encoding.UTF8.GetString(bytes);
            }

            var dictionary = value as Dictionary<string, object>;
            if (dictionary != null)
            {
                var sb = new StringBuilder();

                foreach (var kvp in dictionary)
                {
                    sb.Append(kvp.Key);
                    sb.Append("=");
                    sb.Append(ValueToString(kvp.Value));
                    sb.Append(",");
                }

                if (sb.Length > 0)
                {
                    sb.Remove(sb.Length - 1, 1);
                }

                return sb.ToString();
            }

            var list = value as List<object>;
            if (list != null)
            {
                var sb = new StringBuilder();

                foreach (var entry in list)
                {
                    sb.Append(ValueToString(entry));
                    sb.Append(";");
                }

                if (sb.Length > 0)
                {
                    sb.Remove(sb.Length - 1, 1);
                }

                return sb.ToString();
            }

            var timestamp = value as global::RabbitMQ.Client.AmqpTimestamp?;
            if (timestamp.HasValue)
            {
                return DateTimeExtensions.ToWireFormattedString(UnixEpoch.AddSeconds(timestamp.Value.UnixTime));
            }

            return value?.ToString();
        }

        readonly Func<BasicDeliverEventArgs, string> messageIdStrategy;

        static ILog Logger = LogManager.GetLogger(typeof(MessageConverter));

        static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }
}
