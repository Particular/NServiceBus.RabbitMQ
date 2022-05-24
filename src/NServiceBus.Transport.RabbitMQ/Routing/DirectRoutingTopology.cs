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
        public DirectRoutingTopology(bool useDurableEntities, Func<string> exchangeNameConvention = null, Func<Type, string> routingKeyConvention = null)
        {
            conventions = new Conventions(
                exchangeNameConvention ?? DefaultExchangeNameConvention,
                routingKeyConvention ?? DefaultRoutingKeyConvention.GenerateRoutingKey);
            this.useDurableEntities = useDurableEntities;
        }

        string DefaultExchangeNameConvention() => "amq.topic";

        public void SetupSubscription(IModel channel, MessageMetadata type, string subscriberName)
        {
            CreateExchange(channel, ExchangeName());
            channel.QueueBind(subscriberName, ExchangeName(), GetRoutingKeyForBinding(type.MessageType));
        }

        public void TeardownSubscription(IModel channel, MessageMetadata type, string subscriberName)
        {
            channel.QueueUnbind(subscriberName, ExchangeName(), GetRoutingKeyForBinding(type.MessageType), null);
        }

        public void Publish(IModel channel, Type type, OutgoingMessage message, IBasicProperties properties)
        {
            channel.BasicPublish(ExchangeName(), GetRoutingKeyForPublish(type), false, properties, message.Body);
        }

        public void Send(IModel channel, string address, OutgoingMessage message, IBasicProperties properties)
        {
            channel.BasicPublish(string.Empty, address, true, properties, message.Body);
        }

        public void RawSendInCaseOfFailure(IModel channel, string address, ReadOnlyMemory<byte> body, IBasicProperties properties)
        {
            channel.BasicPublish(string.Empty, address, true, properties, body);
        }

        public void Initialize(IModel channel, IEnumerable<string> receivingAddresses, IEnumerable<string> sendingAddresses, bool useQuorumQueues)
        {
            IDictionary<string, object> queueArguments = null;

            if (useQuorumQueues)
            {
                queueArguments = new Dictionary<string, object> { { "x-queue-type", "quorum" } };
            }


            foreach (var address in receivingAddresses.Concat(sendingAddresses))
            {
                channel.QueueDeclare(address, useDurableEntities, false, false, queueArguments);
            }
        }

        public void BindToDelayInfrastructure(IModel channel, string address, string deliveryExchange, string routingKey)
        {
            channel.QueueBind(address, deliveryExchange, routingKey);
        }

        string ExchangeName() => conventions.ExchangeName();

        void CreateExchange(IModel channel, string exchangeName)
        {
            if (exchangeName == AmqpTopicExchange)
            {
                return;
            }

            try
            {
                channel.ExchangeDeclare(exchangeName, ExchangeType.Topic, useDurableEntities);
            }
            catch (Exception)
            {
                // TODO: Any better way to make this idempotent?
            }
        }

        string GetRoutingKeyForPublish(Type eventType) => conventions.RoutingKey(eventType);

        string GetRoutingKeyForBinding(Type eventType)
        {
            if (eventType == typeof(IEvent) || eventType == typeof(object))
            {
                return "#";
            }

            return conventions.RoutingKey(eventType) + ".#";
        }

        const string AmqpTopicExchange = "amq.topic";

        readonly Conventions conventions;
        readonly bool useDurableEntities;

        public class Conventions
        {
            public Conventions(Func<string> exchangeName, Func<Type, string> routingKey)
            {
                ExchangeName = exchangeName;
                RoutingKey = routingKey;
            }

            public Func<string> ExchangeName { get; }

            public Func<Type, string> RoutingKey { get; }
        }
    }
}