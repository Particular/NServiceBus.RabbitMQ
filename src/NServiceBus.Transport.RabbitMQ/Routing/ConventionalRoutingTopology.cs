#nullable disable
namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using global::RabbitMQ.Client;
    using Unicast.Messages;

    /// <summary>
    /// Implements the RabbitMQ routing topology as described at http://codebetter.com/drusellers/2011/05/08/brain-dump-conventional-routing-in-rabbitmq/
    /// take 4:
    /// <list type="bullet">
    /// <item><description>we generate an exchange for each queue so that we can do direct sends to the queue. it is bound as a fanout exchange</description></item>
    /// <item><description> for each message published we generate series of exchanges that go from concrete class to each of its subclass
    /// / interfaces these are linked together from most specific to least specific. This way if you subscribe to the base interface you get
    /// all the messages. or you can be more selective. all exchanges in this situation are bound as fanouts.</description></item>
    /// <item><description>the subscriber declares his own queue and his queue exchange –
    /// he then also declares/binds his exchange to each of the message type exchanges desired</description></item>
    /// <item><description> the publisher discovers all of the exchanges needed for a given message, binds them all up
    /// and then pushes the message into the most specific queue letting RabbitMQ do the fanout for him. (One publish, multiple receivers!)</description></item>
    /// <item><description>we generate an exchange for each queue so that we can do direct sends to the queue. it is bound as a fanout exchange</description></item>
    /// </list>
    /// </summary>
    class ConventionalRoutingTopology : IRoutingTopology
    {
        public ConventionalRoutingTopology(bool durable, QueueType queueType)
        {
            this.durable = durable;
            this.queueType = queueType;
            exchangeNameConvention = DefaultExchangeNameConvention;
        }

        internal ConventionalRoutingTopology(bool durable, QueueType queueType, Func<Type, string> exchangeNameConvention)
        {
            this.queueType = queueType;
            this.durable = durable;
            this.exchangeNameConvention = exchangeNameConvention;
        }

        static string DefaultExchangeNameConvention(Type type) => type.Namespace + ":" + type.Name;

        public void SetupSubscription(IModel channel, MessageMetadata type, string subscriberName)
        {
            // Make handlers for IEvent handle all events whether they extend IEvent or not
            var typeToSubscribe = type.MessageType != typeof(IEvent) ? type.MessageType : typeof(object);

            SetupTypeSubscriptions(channel, typeToSubscribe);
            channel.ExchangeBind(subscriberName, exchangeNameConvention(typeToSubscribe), string.Empty);
        }

        public void TeardownSubscription(IModel channel, MessageMetadata type, string subscriberName)
        {
            try
            {
                channel.ExchangeUnbind(subscriberName, exchangeNameConvention(type.MessageType), string.Empty, null);
            }
            catch (Exception)
            {
                // TODO: Any better way to make this idempotent?
            }
        }

        public void Publish(IModel channel, Type type, OutgoingMessage message, IBasicProperties properties)
        {
            SetupTypeSubscriptions(channel, type);
            channel.BasicPublish(exchangeNameConvention(type), string.Empty, false, properties, message.Body);
        }

        public void Send(IModel channel, string address, OutgoingMessage message, IBasicProperties properties)
        {
            channel.BasicPublish(address, string.Empty, true, properties, message.Body);
        }

        public void RawSendInCaseOfFailure(IModel channel, string address, ReadOnlyMemory<byte> body, IBasicProperties properties)
        {
            channel.BasicPublish(address, string.Empty, true, properties, body);
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
                CreateExchange(channel, address);
                channel.QueueBind(address, address, string.Empty);
            }
        }

        public void BindToDelayInfrastructure(IModel channel, string address, string deliveryExchange, string routingKey)
        {
            channel.ExchangeBind(address, deliveryExchange, routingKey);
        }

        void SetupTypeSubscriptions(IModel channel, Type type)
        {
            if (type == typeof(object) || IsTypeTopologyKnownConfigured(type))
            {
                return;
            }

            var typeToProcess = type;
            CreateExchange(channel, exchangeNameConvention(typeToProcess));
            var baseType = typeToProcess.BaseType;

            while (baseType != null)
            {
                CreateExchange(channel, exchangeNameConvention(baseType));
                channel.ExchangeBind(exchangeNameConvention(baseType), exchangeNameConvention(typeToProcess), string.Empty);
                typeToProcess = baseType;
                baseType = typeToProcess.BaseType;
            }

            foreach (var interfaceType in type.GetInterfaces())
            {
                var exchangeName = exchangeNameConvention(interfaceType);

                CreateExchange(channel, exchangeName);
                channel.ExchangeBind(exchangeName, exchangeNameConvention(type), string.Empty);
            }

            MarkTypeConfigured(type);
        }

        void MarkTypeConfigured(Type eventType)
        {
            typeTopologyConfiguredSet[eventType] = null;
        }

        bool IsTypeTopologyKnownConfigured(Type eventType) => typeTopologyConfiguredSet.ContainsKey(eventType);

        void CreateExchange(IModel channel, string exchangeName)
        {
            try
            {
                channel.ExchangeDeclare(exchangeName, ExchangeType.Fanout, durable);
            }
            catch (Exception)
            {
                // TODO: Any better way to make this idempotent?
            }
        }

        readonly bool durable;
        readonly QueueType queueType;
        readonly ConcurrentDictionary<Type, string> typeTopologyConfiguredSet = new ConcurrentDictionary<Type, string>();

        Func<Type, string> exchangeNameConvention;
    }
}