namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using global::RabbitMQ.Client;

    class RoutingTopology2Adapter : IRoutingTopology2
    {
        private readonly IRoutingTopology routingTopology;
        private readonly bool durableMessagesEnabled;

        public RoutingTopology2Adapter(IRoutingTopology routingTopology, bool durableMessagesEnabled)
        {
            this.routingTopology = routingTopology;
            this.durableMessagesEnabled = durableMessagesEnabled;
        }

        public void Initialize(IModel channel, IEnumerable<string> addresses)
        {
            foreach (var address in addresses)
            {
                channel.QueueDeclare(address, durableMessagesEnabled, false, false, null);
                this.routingTopology.Initialize(channel, address);
            }
        }

        public void Publish(IModel channel, Type type, OutgoingMessage message, IBasicProperties properties)
        {
            this.routingTopology.Publish(channel, type, message, properties);
        }

        public void RawSendInCaseOfFailure(IModel channel, string address, byte[] body, IBasicProperties properties)
        {
            this.routingTopology.RawSendInCaseOfFailure(channel, address, body, properties);
        }

        public void Send(IModel channel, string address, OutgoingMessage message, IBasicProperties properties)
        {
            this.routingTopology.Send(channel, address, message, properties);
        }

        public void SetupSubscription(IModel channel, Type type, string subscriberName)
        {
            this.routingTopology.SetupSubscription(channel, type, subscriberName);
        }

        public void TeardownSubscription(IModel channel, Type type, string subscriberName)
        {
            this.routingTopology.TeardownSubscription(channel, type, subscriberName);
        }
    }
}
