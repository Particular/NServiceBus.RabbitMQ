namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using Logging;
    using Unicast;

    static class RabbitMqTransportMessageExtensions
    {
        public static void FillRabbitMqProperties(TransportMessage message, DeliveryOptions options, IBasicProperties properties)
        {
            properties.MessageId = message.Id;

            if (!String.IsNullOrEmpty(message.CorrelationId))
            {
                properties.CorrelationId = message.CorrelationId;
            }

            if (message.TimeToBeReceived < TimeSpan.MaxValue)
            {
                properties.Expiration = message.TimeToBeReceived.TotalMilliseconds.ToString();
            }

            properties.SetPersistent(message.Recoverable);

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

            var replyToAddress = options.ReplyToAddress ?? message.ReplyToAddress;
            if (replyToAddress != null)
            {
                properties.ReplyTo = replyToAddress.Queue;
            }
        }

        public static TransportMessage ToTransportMessage(BasicDeliverEventArgs message)
        {
            var properties = message.BasicProperties;

            if (!properties.IsMessageIdPresent() || string.IsNullOrWhiteSpace(properties.MessageId))
            {
                throw new InvalidOperationException("A non empty message_id property is required when running NServiceBus on top of RabbitMq. If this is a interop message please make sure to set the message_id property before publishing the message");
            }

            var headers = DeserializeHeaders(message);



            var result = new TransportMessage(properties.MessageId, headers)
            {
                Body = message.Body ?? new byte[0],
            };

            if (properties.IsReplyToPresent())
            {
                string replyToAddressNSBHeaders;
                var nativeReplyToAddress = properties.ReplyTo;

                if (headers.TryGetValue(Headers.ReplyToAddress, out replyToAddressNSBHeaders) && replyToAddressNSBHeaders != nativeReplyToAddress)
                {
                    Logger.WarnFormat("Missmatching replyto address properties found, the native '{0}' will override the one found in the headers '{1}'",nativeReplyToAddress,replyToAddressNSBHeaders);
                }

                headers[Headers.ReplyToAddress] = nativeReplyToAddress;
            }

            if (properties.IsCorrelationIdPresent())
            {
                result.CorrelationId = properties.CorrelationId;
            }

            //When doing native interop we only require the type to be set the "fullName" of the message
            if (!result.Headers.ContainsKey(Headers.EnclosedMessageTypes) && properties.IsTypePresent())
            {
                result.Headers[Headers.EnclosedMessageTypes] = properties.Type;
            }

            return result;
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

        static ILog Logger = LogManager.GetLogger(typeof(RabbitMqTransportMessageExtensions));
    }
}