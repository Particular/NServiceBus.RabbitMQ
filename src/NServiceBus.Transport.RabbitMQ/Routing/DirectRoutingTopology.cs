﻿namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using global::RabbitMQ.Client;
    using NServiceBus.Logging;

    /// <summary>
    /// Route using a static routing convention for routing messages from publishers to subscribers using routing keys.
    /// </summary>
    class DirectRoutingTopology : IRoutingTopology
    {
        public DirectRoutingTopology(Conventions conventions, bool durable, QueueType queueType)
        {
            this.conventions = conventions;
            this.durable = durable;
            this.queueType = queueType;
        }

        public void SetupSubscription(IModel channel, Type type, string subscriberName)
        {
            CreateExchange(channel, ExchangeName());
            channel.QueueBind(subscriberName, ExchangeName(), GetRoutingKeyForBinding(type));
        }

        public void TeardownSubscription(IModel channel, Type type, string subscriberName)
        {
            channel.QueueUnbind(subscriberName, ExchangeName(), GetRoutingKeyForBinding(type), null);
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
                channel.ExchangeDeclare(exchangeName, ExchangeType.Topic, durable);
            }
            // ReSharper disable EmptyGeneralCatchClause
            catch (Exception)
            // ReSharper restore EmptyGeneralCatchClause
            {

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
        readonly bool durable;
        readonly QueueType queueType;

        static readonly ILog Logger = LogManager.GetLogger(typeof(DirectRoutingTopology));

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