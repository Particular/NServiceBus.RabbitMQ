namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using global::RabbitMQ.Client;
    using Settings;

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
        ReadOnlySettings settings;

        public AutomaticRoutingTopology(ReadOnlySettings settings)
        {
            this.settings = settings;
        }

        public void SetupSubscription(IModel channel, Type type, string subscriberName)
        {
            //throw new Exception("Manual subscribing is not allowed in automatic routing topology.");
        }

        public void TeardownSubscription(IModel channel, Type type, string subscriberName)
        {
            //throw new Exception("Manual unsubscribing is not allowed in automatic routing topology.");
        }

        public void Publish(IModel channel, IOutgoingTransportOperation operation, IBasicProperties properties)
        {
            var op = (MulticastTransportOperation)operation;

            EnsureEventExchangeHierarchyIsConfigured(channel, op.MessageType);
            channel.BasicPublish(PublishExchangeName(op.MessageType), string.Empty, false, properties, op.Message.Body);
        }

        public void Send(IModel channel, IOutgoingTransportOperation operation, IBasicProperties properties)
        {
            var multicastSend = operation as MulticastTransportOperation;
            if (multicastSend != null)
            {
                channel.BasicPublish("Commands", multicastSend.MessageType.FullName, true, properties, operation.Message.Body);
            }
            else
            {
                var unicastSend = operation as UnicastTransportOperation;
                if (unicastSend != null)
                {
                    if (unicastSend.Destination.StartsWith("#type:"))
                    {
                        var typeFullName = unicastSend.Destination.Replace("#type:", "");
                        channel.BasicPublish("Commands", typeFullName, true, properties, operation.Message.Body);
                    }
                    else
                    {
                        channel.BasicPublish(string.Empty, unicastSend.Destination, true, properties, operation.Message.Body);
                    }
                    return;
                }
                throw new Exception("Not supported send operation: " + operation.GetType());
            }
        }

        public void RawSendInCaseOfFailure(IModel channel, string address, byte[] body, IBasicProperties properties)
        {
            channel.BasicPublish(address, String.Empty, true, properties, body);
        }

        public void Initialize(IModel channel, string mainQueue)
        {
            channel.ExchangeDeclare("Commands", "fanout", true);
            channel.ExchangeDeclare($"{mainQueue}-0", "fanout", true);

            var localFanoutExchange = $"Commands-{mainQueue}";
            channel.ExchangeDeclare(localFanoutExchange, "fanout", true, true, null);
            channel.QueueBind(mainQueue, localFanoutExchange, string.Empty);

            var handledTypes = settings.GetAvailableTypes().SelectMany(GetHandledTypes).ToArray();
            foreach (var handledType in handledTypes)
            {
                var commandExchangeArgs = new Dictionary<string, object>
                {
                    ["alternate-exchange"] = $"{mainQueue}-0"
                };
                var typeExchange = SendExchangeName(handledType);

                channel.ExchangeDelete(typeExchange);
                channel.ExchangeDeclare(typeExchange, "direct", true, true, commandExchangeArgs);
            }

            BindMessageTypes(channel, localFanoutExchange, handledTypes);
        }

        static IEnumerable<Type> GetHandledTypes(Type candidateHandlerType)
        {
            if (candidateHandlerType.IsAbstract || candidateHandlerType.IsGenericTypeDefinition)
            {
                return Enumerable.Empty<Type>();
            }

            var handlerInterfaces = candidateHandlerType.GetInterfaces()
                .Where(@interface => @interface.IsGenericType && @interface.GetGenericTypeDefinition() == IHandleMessagesType);

            var handledMessages = handlerInterfaces.Select(i => i.GetGenericArguments()[0]);
            return handledMessages;
        }

        static Type IHandleMessagesType = typeof(IHandleMessages<>);

        public OutboundRoutingPolicy OutboundRoutingPolicy => new OutboundRoutingPolicy(OutboundRoutingType.Multicast, OutboundRoutingType.Multicast, OutboundRoutingType.Unicast);

        static string PublishExchangeName(Type type) => type.Namespace + ":" + type.Name + "_p";
        static string SendExchangeName(Type type) => type.Namespace + ":" + type.Name + "_s";

        public void BindMessageTypes(IModel channel, string localExchange, IEnumerable<Type> messagesHandledByThisEndpoint)
        {
            foreach (var type in messagesHandledByThisEndpoint)
            {
                BindForSending(channel, localExchange, type);
                BindForPublishing(channel, localExchange, type);
            }
        }

        static void BindForSending(IModel channel, string localExchange, Type type)
        {
            var typeExchange = SendExchangeName(type);
            channel.ExchangeBind(typeExchange, "Commands", "", null);
            channel.ExchangeBind(localExchange, typeExchange, type.FullName, null);
        }

        void BindForPublishing(IModel channel, string localExchange, Type type)
        {
            EnsureEventExchangeHierarchyIsConfigured(channel, type);

            channel.ExchangeBind(localExchange, PublishExchangeName(type), string.Empty);
        }

        void EnsureEventExchangeHierarchyIsConfigured(IModel channel, Type type)
        {
            if (IsTypeTopologyKnownConfigured(type))
            {
                return;
            }

            var typeToProcess = type;
            CreateExchange(channel, PublishExchangeName(typeToProcess));
            var baseType = typeToProcess.BaseType;

            while (baseType != null)
            {
                CreateExchange(channel, PublishExchangeName(baseType));
                channel.ExchangeBind(PublishExchangeName(baseType), PublishExchangeName(typeToProcess), string.Empty);
                typeToProcess = baseType;
                baseType = typeToProcess.BaseType;
            }

            foreach (var interfaceType in type.GetInterfaces())
            {
                var exchangeName = PublishExchangeName(interfaceType);

                CreateExchange(channel, exchangeName);
                channel.ExchangeBind(exchangeName, PublishExchangeName(type), string.Empty);
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