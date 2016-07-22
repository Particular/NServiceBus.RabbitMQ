namespace NServiceBus.Transport.RabbitMQ
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Text;
    using global::RabbitMQ.Client.Events;

    static class HeaderConverter
    {
        public static Dictionary<string, string> RetrieveHeaders(BasicDeliverEventArgs message)
        {
            var properties = message.BasicProperties;

            var headers = DeserializeHeaders(properties.Headers);

            if (properties.IsReplyToPresent())
            {
                headers[Headers.ReplyToAddress] = properties.ReplyTo;
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

        static Dictionary<string, string> DeserializeHeaders(IDictionary<string, object> headers)
        {
            var deserializedHeaders = new Dictionary<string, string>();

            if (headers != null)
            {
                var messageHeaders = headers as Dictionary<string, object>
                    ?? new Dictionary<string, object>(headers);

                foreach (var header in messageHeaders)
                {
                    deserializedHeaders.Add(header.Key, header.Value == null ? null : ValueToString(header.Value));
                }
            }

            return deserializedHeaders;
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

            var dictionary = value as IDictionary<string, object>;
            if (dictionary != null)
            {
                var valuesToJoin = new List<string>();
                // ReSharper disable once LoopCanBeConvertedToQuery
                foreach (var kvp in dictionary)
                {
                    valuesToJoin.Add(string.Concat(kvp.Key, "=", ValueToString(kvp.Value)));
                }
                return string.Join(",", valuesToJoin);
            }

            var list = value as IList;
            if (list != null)
            {
                var valuesToJoin = new List<string>();
                foreach (var entry in list)
                {
                    valuesToJoin.Add(ValueToString(entry));
                }
                return string.Join(";", valuesToJoin);
            }

            return null;
        }
    }
}