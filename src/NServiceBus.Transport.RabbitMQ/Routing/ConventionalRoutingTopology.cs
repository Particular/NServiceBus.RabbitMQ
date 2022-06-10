#nullable disable
namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using global::RabbitMQ.Client;
    using NServiceBus.Logging;
    using Unicast.Messages;

    class ConventionalRoutingTopology : IRoutingTopology
    {
        public ConventionalRoutingTopology(bool durable, QueueType queueType)
        {
            this.durable = durable;
            this.queueType = queueType;
            exchangeNameConvention = type => type.Namespace + ":" + type.Name;
        }

        internal ConventionalRoutingTopology(bool durable, QueueType queueType, Func<Type, string> exchangeNameConvention)
        {
            this.queueType = queueType;
            this.durable = durable;
            this.exchangeNameConvention = exchangeNameConvention;
        }

        public void SetupSubscription(IModel channel, MessageMetadata type, string subscriberName)
        {
            // Make handlers for IEvent handle all events whether they extend IEvent or not
            var typeToSubscribe = type.MessageType == typeof(IEvent) ? typeof(object) : type.MessageType;

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
            var createDurableQueue = durable;

            if (queueType == QueueType.Quorum)
            {
                arguments = new Dictionary<string, object> { { "x-queue-type", "quorum" } };

                if (createDurableQueue == false)
                {
                    createDurableQueue = true;
                    Logger.Warn("Quorum queues are always durable, so the non-durable setting is being ignored for queue declaration.");
                }
            }

            foreach (var address in receivingAddresses.Concat(sendingAddresses))
            {
                channel.QueueDeclare(address, createDurableQueue, false, false, arguments);
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
        readonly Func<Type, string> exchangeNameConvention;

        static readonly ILog Logger = LogManager.GetLogger(typeof(ConventionalRoutingTopology));
    }
}