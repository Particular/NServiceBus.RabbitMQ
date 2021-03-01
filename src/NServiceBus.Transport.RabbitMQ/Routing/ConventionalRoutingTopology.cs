
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
    public class ConventionalRoutingTopology : IRoutingTopology
    {
        /// <summary>
        /// Creates a new instance of conventional routing topology.
        /// </summary>
        /// <param name="useDurableExchanges">Indicates whether exchanges and queues declared by the routing topology should be durable.</param>
        public ConventionalRoutingTopology(bool useDurableExchanges)
        {
            this.useDurableExchanges = useDurableExchanges;
            exchangeNameConvention = DefaultExchangeNameConvention;
        }

        internal ConventionalRoutingTopology(bool useDurableExchanges, Func<Type, string> exchangeNameConvention)
        {
            this.useDurableExchanges = useDurableExchanges;
            this.exchangeNameConvention = exchangeNameConvention;
        }

        static string DefaultExchangeNameConvention(Type type) => type.Namespace + ":" + type.Name;

        /// <summary>
        /// Sets up a subscription for the subscriber to the specified type.
        /// </summary>
        /// <param name="channel">The RabbitMQ channel to operate on.</param>
        /// <param name="type">The type to subscribe to.</param>
        /// <param name="subscriberName">The name of the subscriber.</param>
        public void SetupSubscription(IModel channel, MessageMetadata type, string subscriberName)
        {
            // Make handlers for IEvent handle all events whether they extend IEvent or not
            var typeToSubscribe = type.MessageType != typeof(IEvent) ? type.MessageType : typeof(object);

            SetupTypeSubscriptions(channel, typeToSubscribe);
            channel.ExchangeBind(subscriberName, exchangeNameConvention(typeToSubscribe), string.Empty);
        }

        /// <summary>
        /// Removes a subscription for the subscriber to the specified type.
        /// </summary>
        /// <param name="channel">The RabbitMQ channel to operate on.</param>
        /// <param name="type">The type to unsubscribe from.</param>
        /// <param name="subscriberName">The name of the subscriber.</param>
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

        /// <summary>
        /// Publishes a message of the specified type.
        /// </summary>
        /// <param name="channel">The RabbitMQ channel to operate on.</param>
        /// <param name="type">The type of the message to be published.</param>
        /// <param name="message">The message to publish.</param>
        /// <param name="properties">The RabbitMQ properties of the message to publish.</param>
        public void Publish(IModel channel, Type type, OutgoingMessage message, IBasicProperties properties)
        {
            SetupTypeSubscriptions(channel, type);
            channel.BasicPublish(exchangeNameConvention(type), string.Empty, false, properties, message.Body);
        }

        /// <summary>
        /// Sends a message to the specified endpoint.
        /// </summary>
        /// <param name="channel">The RabbitMQ channel to operate on.</param>
        /// <param name="address">The address of the destination endpoint.</param>
        /// <param name="message">The message to send.</param>
        /// <param name="properties">The RabbitMQ properties of the message to send.</param>
        public void Send(IModel channel, string address, OutgoingMessage message, IBasicProperties properties)
        {
            channel.BasicPublish(address, string.Empty, true, properties, message.Body);
        }

        /// <summary>
        /// Sends a raw message body to the specified endpoint.
        /// </summary>
        /// <param name="channel">The RabbitMQ channel to operate on.</param>
        /// <param name="address">The address of the destination endpoint.</param>
        /// <param name="body">The raw message body to send.</param>
        /// <param name="properties">The RabbitMQ properties of the message to send.</param>
        public void RawSendInCaseOfFailure(IModel channel, string address, ReadOnlyMemory<byte> body, IBasicProperties properties)
        {
            channel.BasicPublish(address, string.Empty, true, properties, body);
        }

        /// <summary>
        /// Declares queues and performs any other initialization logic needed (e.g. creating exchanges and bindings).
        /// </summary>
        /// <param name="channel">The RabbitMQ channel to operate on.</param>
        /// <param name="receivingAddresses">
        /// The addresses of the queues to declare and perform initialization for, that this endpoint is receiving from.
        /// </param>
        /// <param name="sendingAddresses">
        /// The addresses of the queues to declare and perform initialization for, that this endpoint is sending to.
        /// </param>
        public void Initialize(IModel channel, IEnumerable<string> receivingAddresses, IEnumerable<string> sendingAddresses)
        {
            foreach (var address in receivingAddresses.Concat(sendingAddresses))
            {
                channel.QueueDeclare(address, useDurableExchanges, false, false, null);
                CreateExchange(channel, address);
                channel.QueueBind(address, address, string.Empty);
            }
        }

        /// <summary>
        /// Binds an address to the delay infrastructure's delivery exchange.
        /// </summary>
        /// <param name="channel">The RabbitMQ channel to operate on.</param>
        /// <param name="address">The address that needs to be bound to the delivery exchange.</param>
        /// <param name="deliveryExchange">The name of the delivery exchange.</param>
        /// <param name="routingKey">The routing key required for the binding.</param>
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
                channel.ExchangeDeclare(exchangeName, ExchangeType.Fanout, useDurableExchanges);
            }
            catch (Exception)
            {
                // TODO: Any better way to make this idempotent?
            }
        }

        readonly bool useDurableExchanges;
        readonly ConcurrentDictionary<Type, string> typeTopologyConfiguredSet = new ConcurrentDictionary<Type, string>();
        Func<Type, string> exchangeNameConvention;
    }
}