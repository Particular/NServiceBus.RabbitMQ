namespace NServiceBus.Transports.RabbitMQ.Tests
{
    using System;
    using System.Collections.Generic;
    using NServiceBus.DeliveryConstraints;
    using NServiceBus.Performance.TimeToBeReceived;
    using NServiceBus.Routing;

    public class OutgoingMessageBuilder
    {
        string _messageId = Guid.NewGuid().ToString();
        byte[] _body;
        Dictionary<string, string> _headers = new Dictionary<string, string>();
        IList<DeliveryConstraint> _constraints = new List<DeliveryConstraint>();
        DispatchConsistency _dispatchConsistency = DispatchConsistency.Default;
        AddressTag _addressTag;

        public OutgoingMessageBuilder WithBody(byte[] body)
        {
            _body = body;
            return this;
        }

        public TransportOperation Build()
        {
            return new TransportOperation(
                new OutgoingMessage(_messageId, _headers, _body),
                new DispatchOptions(_addressTag, _dispatchConsistency, _constraints)
            );
        }

        public OutgoingMessageBuilder SendTo(string unicastAddress)
        {
            _addressTag = new UnicastAddressTag(unicastAddress);
            return this;
        }

        public OutgoingMessageBuilder PublishType(Type messageType)
        {
            _addressTag = new MulticastAddressTag(messageType);
            return this;
        }

        public OutgoingMessageBuilder WithHeader(string key,string value)
        {
            _headers[key] = value;
            return this;
        }

        public OutgoingMessageBuilder TimeToBeReceived(TimeSpan timeToBeReceived)
        {
            _constraints.Add(new DiscardIfNotReceivedBefore(timeToBeReceived));
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
            _constraints.Add(new NonDurableDelivery());
            return this;
        }

        public OutgoingMessageBuilder WithIntent(MessageIntentEnum intent)
        {
            return WithHeader(Headers.MessageIntent, intent.ToString());
        }
    }
}