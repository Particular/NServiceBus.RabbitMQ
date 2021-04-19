﻿
namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using global::RabbitMQ.Client;
    using Unicast.Messages;


    /// <summary>
    /// Route using a static routing convention for routing messages from publishers to subscribers using routing keys.
    /// </summary>
    public class DirectRoutingTopology : IRoutingTopology
    {
        /// <summary>
        /// Creates a new instance of DirectRoutingTopology,
        /// </summary>
        /// <param name="useDurableEntities">Indicates whether exchanges and queues declared by the routing topology should be durable.</param>
        /// <param name="exchangeNameConvention">Exchange name convention.</param>
        /// <param name="routingKeyConvention">Routing key convention.</param>
        public DirectRoutingTopology(bool useDurableEntities, Func<string> exchangeNameConvention = null, Func<Type, string> routingKeyConvention = null)
        {
            conventions = new Conventions(
                exchangeNameConvention ?? DefaultExchangeNameConvention,
                routingKeyConvention ?? DefaultRoutingKeyConvention.GenerateRoutingKey);
            this.useDurableEntities = useDurableEntities;
        }

        string DefaultExchangeNameConvention() => "amq.topic";

        /// <summary>
        /// Sets up a subscription for the subscriber to the specified type.
        /// </summary>
        /// <param name="channel">The RabbitMQ channel to operate on.</param>
        /// <param name="type">The type to subscribe to.</param>
        /// <param name="subscriberName">The name of the subscriber.</param>
        public void SetupSubscription(IModel channel, MessageMetadata type, string subscriberName)
        {
            CreateExchange(channel, ExchangeName());
            channel.QueueBind(subscriberName, ExchangeName(), GetRoutingKeyForBinding(type.MessageType));
        }

        /// <summary>
        /// Removes a subscription for the subscriber to the specified type.
        /// </summary>
        /// <param name="channel">The RabbitMQ channel to operate on.</param>
        /// <param name="type">The type to unsubscribe from.</param>
        /// <param name="subscriberName">The name of the subscriber.</param>
        public void TeardownSubscription(IModel channel, MessageMetadata type, string subscriberName)
        {
            channel.QueueUnbind(subscriberName, ExchangeName(), GetRoutingKeyForBinding(type.MessageType), null);
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
            channel.BasicPublish(ExchangeName(), GetRoutingKeyForPublish(type), false, properties, message.Body);
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
            channel.BasicPublish(string.Empty, address, true, properties, message.Body);
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
            channel.BasicPublish(string.Empty, address, true, properties, body);
        }

        /// <summary>
        /// Declares queues and performs any other initialization logic needed (e.g. creating exchanges and bindings).
        /// </summary>
        /// <param name="connection">The RabbitMQ connection to operate on.</param>
        /// <param name="receivingAddresses">
        /// The addresses of the queues to declare and perform initialization for, that this endpoint is receiving from.
        /// </param>
        /// <param name="sendingAddresses">
        /// The addresses of the queues to declare and perform initialization for, that this endpoint is sending to.
        /// </param>
        /// <param name="useQuorumQueues">
        /// Should the queues that this endpoint receieves from be created as quorum queues.
        /// </param>
        /// <param name="allowInputQueueConfigurationMismatch">
        /// If the defined receiving queues already exists, the endpoint should fail if the existing queues are configured with different settings unless <paramref name="allowInputQueueConfigurationMismatch"/> is set to <code>true</code>.
        /// </param>
        public void Initialize(IConnection connection, IEnumerable<string> receivingAddresses, IEnumerable<string> sendingAddresses, bool useQuorumQueues, bool allowInputQueueConfigurationMismatch)
        {
            IDictionary<string, object> queueArguments = null;
            if (useQuorumQueues)
            {
                queueArguments = new Dictionary<string, object> { { "x-queue-type", "quorum" } };
            }

            using (var channel = connection.CreateModel())
            {
                foreach (var address in receivingAddresses)
                {
                    if (!allowInputQueueConfigurationMismatch || !QueueHelper.QueueExists(connection, address))
                    {
                        channel.QueueDeclare(address, useDurableEntities, false, false, queueArguments);
                    }
                }

                foreach (var sendingAddress in sendingAddresses)
                {
                    if (!QueueHelper.QueueExists(connection, sendingAddress))
                    {
                        channel.QueueDeclare(sendingAddress, useDurableEntities, false, false, queueArguments);
                    }
                }
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

        class Conventions
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