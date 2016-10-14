namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Concurrent;
    using global::RabbitMQ.Client;

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
    class AutomaticRoutingTopology : IRoutingTopology
    {
        public void SetupSubscription(IModel channel, Type type, string subscriberName)
        {
            if (type == typeof(IEvent))
            {
                // Make handlers for IEvent handle all events whether they extend IEvent or not
                type = typeof(object);
            }

            SetupTypeSubscriptions(channel, type);
            channel.ExchangeBind(subscriberName, ExchangeName(type), string.Empty);
        }

        public void TeardownSubscription(IModel channel, Type type, string subscriberName)
        {
            try
            {
                channel.ExchangeUnbind(subscriberName, ExchangeName(type), string.Empty, null);
            }
            // ReSharper disable EmptyGeneralCatchClause
            catch (Exception)
            // ReSharper restore EmptyGeneralCatchClause
            {
                // TODO: Any better way to make this idempotent?
            }
        }

        public void Publish(IModel channel, IOutgoingTransportOperation operation, IBasicProperties properties)
        {
            var op = (MulticastTransportOperation) operation;

            SetupTypeSubscriptions(channel, op.MessageType);
            channel.BasicPublish(ExchangeName(op.MessageType), String.Empty, false, properties, op.Message.Body);
        }

        public void Send(IModel channel, IOutgoingTransportOperation operation, IBasicProperties properties)
        {
            var op = (MulticastTransportOperation)operation;
            properties.Headers["Type"] = op.MessageType.AssemblyQualifiedName;
            channel.BasicPublish("Commands", String.Empty, true, properties, op.Message.Body);
        }

        public void RawSendInCaseOfFailure(IModel channel, string address, byte[] body, IBasicProperties properties)
        {
            channel.BasicPublish(address, String.Empty, true, properties, body);
        }

        public void Initialize(IModel channel, string mainQueue)
        {
            CreateExchange(channel, mainQueue);
            channel.QueueBind(mainQueue, mainQueue, string.Empty);
        }

        public OutboundRoutingPolicy OutboundRoutingPolicy => new OutboundRoutingPolicy(OutboundRoutingType.Multicast, OutboundRoutingType.Multicast, OutboundRoutingType.Unicast);

        static string ExchangeName(Type type) => type.Namespace + ":" + type.Name;

        void SetupTypeSubscriptions(IModel channel, Type type)
        {
            if (type == typeof(Object) || IsTypeTopologyKnownConfigured(type))
            {
                return;
            }

            var typeToProcess = type;
            CreateExchange(channel, ExchangeName(typeToProcess));
            var baseType = typeToProcess.BaseType;

            while (baseType != null)
            {
                CreateExchange(channel, ExchangeName(baseType));
                channel.ExchangeBind(ExchangeName(baseType), ExchangeName(typeToProcess), string.Empty);
                typeToProcess = baseType;
                baseType = typeToProcess.BaseType;
            }

            foreach (var interfaceType in type.GetInterfaces())
            {
                var exchangeName = ExchangeName(interfaceType);

                CreateExchange(channel, exchangeName);
                channel.ExchangeBind(exchangeName, ExchangeName(type), string.Empty);
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
                channel.ExchangeDeclare(exchangeName, ExchangeType.Fanout, true);
            }
            // ReSharper disable EmptyGeneralCatchClause
            catch (Exception)
            // ReSharper restore EmptyGeneralCatchClause
            {
                // TODO: Any better way to make this idempotent?
            }
        }

        readonly ConcurrentDictionary<Type, string> typeTopologyConfiguredSet = new ConcurrentDictionary<Type, string>();
    }
}