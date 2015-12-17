namespace NServiceBus.Transports.RabbitMQ.Tests
{
    using System;
    using System.Collections.Generic;
    using NServiceBus.DeliveryConstraints;
    using NServiceBus.Performance.TimeToBeReceived;
    using NServiceBus.Routing;

    public class TransportMessageBuilder
    {
        string _messageId = Guid.NewGuid().ToString();
        byte[] _body;
        Dictionary<string, string> _headers = new Dictionary<string, string>();
        IList<DeliveryConstraint> _constraints = new List<DeliveryConstraint>(); 
        DispatchConsistency _dispatchConsistency = DispatchConsistency.Default;
        AddressTag _addressTag;

        public TransportMessageBuilder WithBody(byte[] body)
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

        public TransportMessageBuilder SendTo(string unicastAddress)
        {
            _addressTag = new UnicastAddressTag(unicastAddress);
            return this;
        }

        public TransportMessageBuilder WithHeader(string key,string value)
        {
            _headers[key] = value;
            return this;
        }

        public TransportMessageBuilder TimeToBeReceived(TimeSpan timeToBeReceived)
        {
            _constraints.Add(new DiscardIfNotReceivedBefore(timeToBeReceived));
            return this;
        }

        public TransportMessageBuilder ReplyToAddress(string address)
        {
            return WithHeader(Headers.ReplyToAddress, address);
        }

        public TransportMessageBuilder CorrelationId(string correlationId)
        {
            return WithHeader(Headers.CorrelationId, correlationId);
        }

        public TransportMessageBuilder NonDurable()
        {
            _constraints.Add(new NonDurableDelivery());
            return this;
        }

        public TransportMessageBuilder WithIntent(MessageIntentEnum intent)
        {
            return WithHeader(Headers.MessageIntent, intent.ToString());
        }
    }
}