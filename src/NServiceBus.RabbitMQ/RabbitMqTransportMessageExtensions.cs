namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;

    public static class RabbitMqTransportMessageExtensions
    {
        public static IBasicProperties FillRabbitMqProperties(TransportMessage message, IBasicProperties properties)
        {
            properties.MessageId = message.Id;

            properties.CorrelationId = message.CorrelationId;

            if (message.TimeToBeReceived < TimeSpan.MaxValue)
                properties.Expiration = message.TimeToBeReceived.TotalMilliseconds.ToString();

            properties.SetPersistent(message.Recoverable);

            properties.Headers = message.Headers;

            if (message.Headers.ContainsKey(Headers.EnclosedMessageTypes))
            {
                properties.Type = message.Headers[Headers.EnclosedMessageTypes].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            }

            if (message.Headers.ContainsKey(Headers.ContentType))
                properties.ContentType = message.Headers[Headers.ContentType];
            else
            {
                properties.ContentType = "application/octet-stream";
            }
            

            if (message.ReplyToAddress != null && message.ReplyToAddress != Address.Undefined)
                properties.ReplyTo = message.ReplyToAddress.Queue;

            return properties;
        }

        public static TransportMessage ToTransportMessage(BasicDeliverEventArgs message)
        {
            var properties = message.BasicProperties;

            if (!properties.IsMessageIdPresent() || string.IsNullOrWhiteSpace(properties.MessageId))
                throw new InvalidOperationException("A non empty message_id property is required when running NServiceBus on top of RabbitMq. If this is a interop message please make sure to set the message_id property before publishing the message");

            var headers = DeserializeHeaders(message);

            var result = new TransportMessage(properties.MessageId, headers)
                {
                    Body = message.Body,
                };

            if (properties.IsReplyToPresent())
                result.ReplyToAddress = Address.Parse(properties.ReplyTo);

            if (properties.IsCorrelationIdPresent())
                result.CorrelationId = properties.CorrelationId;

            //When doing native interop we only require the type to be set the "fullName" of the message
            if (!result.Headers.ContainsKey(Headers.EnclosedMessageTypes) && properties.IsTypePresent())
            {
                result.Headers[Headers.EnclosedMessageTypes] = properties.Type;
            }

            return result;
        }

        static Dictionary<string,string> DeserializeHeaders(BasicDeliverEventArgs message)
        {
            if (message.BasicProperties.Headers == null)
            {
                return new Dictionary<string, string>();
            }

            return message.BasicProperties.Headers.Cast<DictionaryEntry>()
                        .ToDictionary(
                            dictionaryEntry => (string)dictionaryEntry.Key,
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
                returnValue = value as string;
            }
            else if (value is byte[])
            {
                returnValue = Encoding.UTF8.GetString(value as byte[]);
            }
            else if (value is IDictionary<string, object>)
            {
                var dict = value as IDictionary<string, object>;
                returnValue = String.Join(",", dict.Select(kvp => kvp.Key + "=" + ValueToString(kvp.Value)));
            }
            else if (value is IList)
            {
                var list = value as IList;
                returnValue = String.Join(";", list.Cast<object>().Select(ValueToString));
            }
            return returnValue;
        }
    }

}