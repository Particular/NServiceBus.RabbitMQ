namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
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

        public string RetrieveMessageId(BasicDeliverEventArgs message)
        {
            return messageIdStrategy(message);
        }

        public Dictionary<string, string> RetrieveHeaders(BasicDeliverEventArgs message)
        {
            var properties = message.BasicProperties;

            var headers = DeserializeHeaders(message);

            if (properties.IsReplyToPresent())
            {
                string replyToAddressNSBHeaders;
                var nativeReplyToAddress = properties.ReplyTo;

                if (headers.TryGetValue(Headers.ReplyToAddress, out replyToAddressNSBHeaders))
                {
                    if (replyToAddressNSBHeaders != nativeReplyToAddress)
                    {
                        Logger.WarnFormat("Missmatching replyto address properties found, the address specified by the NServiceBus headers '{1}' will override the native one '{0}'", nativeReplyToAddress, replyToAddressNSBHeaders);
                    }
                }
                else
                {
                    //promote the native address
                    headers[Headers.ReplyToAddress] = nativeReplyToAddress;
                }
            }

            if (properties.IsCorrelationIdPresent())
            {
                headers[Headers.CorrelationId] = properties.CorrelationId;
            }

            //When doing native interop we only require the type to be set the "fullName" of the message
            if (!headers.ContainsKey(Headers.EnclosedMessageTypes) && properties.IsTypePresent())
            {
                headers[Headers.EnclosedMessageTypes] = properties.Type;
            }

            if (properties.IsDeliveryModePresent())
            {
                headers[Headers.NonDurableMessage] = (properties.DeliveryMode == 1).ToString();
            }

            if (headers.ContainsKey("NServiceBus.RabbitMQ.CallbackQueue"))
            {
                headers[Headers.ReplyToAddress] = headers["NServiceBus.RabbitMQ.CallbackQueue"];
            }

            return headers;
        }

        string DefaultMessageIdStrategy(BasicDeliverEventArgs message)
        {
            var properties = message.BasicProperties;

            if (!properties.IsMessageIdPresent() || string.IsNullOrWhiteSpace(properties.MessageId))
            {
                throw new InvalidOperationException("A non empty message_id property is required when running NServiceBus on top of RabbitMq. If this is a interop message please make sure to set the message_id property before publishing the message");
            }

            return properties.MessageId;
        }

        static Dictionary<string, string> DeserializeHeaders(BasicDeliverEventArgs message)
        {
            if (message.BasicProperties.Headers == null)
            {
                return new Dictionary<string, string>();
            }

            return message.BasicProperties.Headers
                .ToDictionary(
                    dictionaryEntry => dictionaryEntry.Key,
                    dictionaryEntry =>
                    {
                        var value = dictionaryEntry.Value;
                        return dictionaryEntry.Value == null ? null : ValueToString(value);
                    });
        }

        static string ValueToString(object value)
        {
            var s = value as string;
            if (s != null)
            {
                return s;
            }

            var bytes = value as byte[];
            if (bytes != null)
            {
                return Encoding.UTF8.GetString(bytes);
            }

            var objects = value as IDictionary<string, object>;
            if (objects != null)
            {
                var dict = objects;
                return String.Join(",", dict.Select(kvp => kvp.Key + "=" + ValueToString(kvp.Value)));
            }

            var list1 = value as IList;
            if (list1 != null)
            {
                var list = list1;
                return String.Join(";", list.Cast<object>().Select(ValueToString));
            }

            return null;
        }

        readonly Func<BasicDeliverEventArgs, string> messageIdStrategy;


        static ILog Logger = LogManager.GetLogger(typeof(MessageConverter));
    }
}