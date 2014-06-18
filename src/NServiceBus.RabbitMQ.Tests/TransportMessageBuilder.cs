namespace NServiceBus.Transports.RabbitMQ.Tests
{
    using System;
    using System.Collections.Generic;

    public class TransportMessageBuilder
    {
        TransportMessage message = new TransportMessage{Recoverable = true};
    
        public TransportMessageBuilder WithBody(byte[] body)
        {
            message.Body = body;
            return this;
        }

        public TransportMessage Build()
        {
            return message;
        }

        public TransportMessageBuilder WithHeader(string key,string value)
        {
            message.Headers[key] = value;
            return this;
        }

        public TransportMessageBuilder TimeToBeReceived(TimeSpan timeToBeReceived)
        {

            message.TimeToBeReceived = timeToBeReceived;
            return this;
        }

        public TransportMessageBuilder ReplyToAddress(Address address)
        {
            message = new TransportMessage(Guid.NewGuid().ToString(),new Dictionary<string, string>()){Recoverable = true};
            message.Headers[Headers.ReplyToAddress] = address.ToString();
            return this;
        }

        public TransportMessageBuilder CorrelationId(string correlationId)
        {
            message.CorrelationId = correlationId;
            return this;
        }
    }
}