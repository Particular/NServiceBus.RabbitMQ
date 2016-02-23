namespace NServiceBus.Transports.RabbitMQ.Tests
{
    using System;
    using System.Collections.Generic;
    using NServiceBus.DeliveryConstraints;
    using NServiceBus.Performance.TimeToBeReceived;
    using NServiceBus.Routing;

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

            for (int i = 0; i < copies; i++)
            {
                if (eventType != null)
                {
                    transportOperations.Add(new TransportOperation(message, new MulticastAddressTag(eventType), dispatchConsistency, constraints));
                }

                if (!string.IsNullOrEmpty(destination))
                {
                    transportOperations.Add(new TransportOperation(message, new UnicastAddressTag(destination), dispatchConsistency, constraints));
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