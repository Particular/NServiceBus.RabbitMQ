namespace NServiceBus.Transports.RabbitMQ.Tests
{
    using System;
    using System.Collections.Generic;
    using NServiceBus.DeliveryConstraints;
    using NServiceBus.Performance.TimeToBeReceived;

    public class OutgoingMessageBuilder
    {
        public OutgoingMessageBuilder WithBody(byte[] body)
        {
            this.body = body;
            return this;
        }

        public TransportOperations Build()
        {
            var message = new OutgoingMessage(messageId, headers, body);

            var multicastOps = new List<MulticastTransportOperation>();

            if (eventType != null)
            {
                multicastOps.Add(new MulticastTransportOperation(message, eventType, constraints, dispatchConsistency));
            }
            var unicastOps = new List<UnicastTransportOperation>();

            if (!string.IsNullOrEmpty(destination))
            {
                unicastOps.Add(new UnicastTransportOperation(message, destination, constraints, dispatchConsistency));
            }

            return new TransportOperations(multicastOps, unicastOps);
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
            constraints.Add(new DiscardIfNotReceivedBefore(timeToBeReceived));
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

        public OutgoingMessageBuilder NonDurable()
        {
            constraints.Add(new NonDurableDelivery());
            return this;
        }

        public OutgoingMessageBuilder WithIntent(MessageIntentEnum intent)
        {
            return WithHeader(Headers.MessageIntent, intent.ToString());
        }

        string destination;
        Type eventType;
        string messageId = Guid.NewGuid().ToString();
        byte[] body;
        Dictionary<string, string> headers = new Dictionary<string, string>();
        IList<DeliveryConstraint> constraints = new List<DeliveryConstraint>();
        DispatchConsistency dispatchConsistency = DispatchConsistency.Default;
    }
}