#nullable disable
namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using global::RabbitMQ.Client;
    using Unicast.Messages;

    class DirectRoutingTopology : IRoutingTopology
    {
        public DirectRoutingTopology(bool durable, QueueType queueType, Func<Type, string> routingKeyConvention = null, Func<string> exchangeNameConvention = null)
        {
            this.durable = durable;
            this.queueType = queueType;
            this.routingKeyConvention = routingKeyConvention ?? DefaultRoutingKeyConvention.GenerateRoutingKey;
            this.exchangeNameConvention = exchangeNameConvention ?? (() => amqpTopicExchange);
        }

        public void SetupSubscription(IModel channel, MessageMetadata type, string subscriberName)
        {
            CreateExchange(channel, exchangeNameConvention());
            channel.QueueBind(subscriberName, exchangeNameConvention(), GetRoutingKeyForBinding(type.MessageType));
        }

        public void TeardownSubscription(IModel channel, MessageMetadata type, string subscriberName)
        {
            channel.QueueUnbind(subscriberName, exchangeNameConvention(), GetRoutingKeyForBinding(type.MessageType), null);
        }

        public void Publish(IModel channel, Type type, OutgoingMessage message, IBasicProperties properties)
        {
            channel.BasicPublish(exchangeNameConvention(), GetRoutingKeyForPublish(type), false, properties, message.Body);
        }

        public void Send(IModel channel, string address, OutgoingMessage message, IBasicProperties properties)
        {
            channel.BasicPublish(string.Empty, address, true, properties, message.Body);
        }

        public void RawSendInCaseOfFailure(IModel channel, string address, ReadOnlyMemory<byte> body, IBasicProperties properties)
        {
            channel.BasicPublish(string.Empty, address, true, properties, body);
        }

        public void Initialize(IModel channel, IEnumerable<string> receivingAddresses, IEnumerable<string> sendingAddresses)
        {
            Dictionary<string, object> arguments = null;

            if (queueType == QueueType.Quorum)
            {
                arguments = new Dictionary<string, object> { { "x-queue-type", "quorum" } };
            }

            foreach (var address in receivingAddresses.Concat(sendingAddresses))
            {
                channel.QueueDeclare(address, durable, false, false, arguments);
            }
        }

        public void BindToDelayInfrastructure(IModel channel, string address, string deliveryExchange, string routingKey)
        {
            channel.QueueBind(address, deliveryExchange, routingKey);
        }

        void CreateExchange(IModel channel, string exchangeName)
        {
            if (exchangeName == amqpTopicExchange)
            {
                return;
            }

            try
            {
                channel.ExchangeDeclare(exchangeName, ExchangeType.Topic, durable);
            }
            catch (Exception)
            {
                // TODO: Any better way to make this idempotent?
            }
        }

        string GetRoutingKeyForPublish(Type eventType) => routingKeyConvention(eventType);

        string GetRoutingKeyForBinding(Type eventType)
        {
            if (eventType == typeof(IEvent) || eventType == typeof(object))
            {
                return "#";
            }

            return routingKeyConvention(eventType) + ".#";
        }

        const string amqpTopicExchange = "amq.topic";

        readonly bool durable;
        readonly QueueType queueType;
        readonly Func<Type, string> routingKeyConvention;
        readonly Func<string> exchangeNameConvention;
    }
}