﻿namespace NServiceBus.Transports.RabbitMQ
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

        public MessageConverter(Func<BasicDeliverEventArgs,string> messageIdStrategy)
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
            var returnValue = default(string);
            if (value is string)
            {
                returnValue = (string)value;
            }
            else if (value is byte[])
            {
                returnValue = Encoding.UTF8.GetString((byte[]) value);
            }
            else if (value is IDictionary<string, object>)
            {
                var dict = (IDictionary<string, object>) value;
                returnValue = String.Join(",", dict.Select(kvp => kvp.Key + "=" + ValueToString(kvp.Value)));
            }
            else if (value is IList)
            {
                var list = (IList) value;
                returnValue = String.Join(";", list.Cast<object>().Select(ValueToString));
            }
            return returnValue;
        }

        readonly Func<BasicDeliverEventArgs, string> messageIdStrategy;


        static ILog Logger = LogManager.GetLogger(typeof(MessageConverter));
    }
}