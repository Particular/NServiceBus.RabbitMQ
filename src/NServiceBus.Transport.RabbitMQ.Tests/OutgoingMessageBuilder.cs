namespace NServiceBus.Transport.RabbitMQ.Tests
{
    using System;
    using System.Collections.Generic;
    using Performance.TimeToBeReceived;
    using Routing;

    public class OutgoingMessageBuilder
    {
        public OutgoingMessageBuilder WithBody(byte[] body)
        {
            this.body = body;
            return this;
        }

        public TransportOperations Build(int copies = 1)
        {
            var message = new OutgoingMessage(messageId, headers, body);

            var transportOperations = new List<TransportOperation>();

            for (var i = 0; i < copies; i++)
            {
                if (eventType != null)
                {
                    transportOperations.Add(new TransportOperation(message, new MulticastAddressTag(eventType), constraints, dispatchConsistency));
                }

                if (!string.IsNullOrEmpty(destination))
                {
                    transportOperations.Add(new TransportOperation(message, new UnicastAddressTag(destination), constraints, dispatchConsistency));
                }
            }

            return new TransportOperations(transportOperations.ToArray());
        }

        public OutgoingMessageBuilder SendTo(string unicastAddress)
        {
            destination = unicastAddress;
            return this;
        }

        public OutgoingMessageBuilder PublishType(Type messageType)
        {
            eventType = messageType;
            return this;
        }

        public OutgoingMessageBuilder WithHeader(string key, string value)
        {
            headers[key] = value;
            return this;
        }

        public OutgoingMessageBuilder TimeToBeReceived(TimeSpan timeToBeReceived)
        {
            constraints.DiscardIfNotReceivedBefore = new DiscardIfNotReceivedBefore(timeToBeReceived);
            return this;
        }

        public OutgoingMessageBuilder ReplyToAddress(string address)
        {
            return WithHeader(Headers.ReplyToAddress, address);
        }

        public OutgoingMessageBuilder CorrelationId(string correlationId)
        {
            return WithHeader(Headers.CorrelationId, correlationId);
        }

        public OutgoingMessageBuilder WithIntent(MessageIntent intent)
        {
            return WithHeader(Headers.MessageIntent, intent.ToString());
        }

        string destination;
        Type eventType;
        string messageId = Guid.NewGuid().ToString();
        byte[] body;
        Dictionary<string, string> headers = [];
        DispatchProperties constraints = [];
        DispatchConsistency dispatchConsistency = DispatchConsistency.Default;
    }
}