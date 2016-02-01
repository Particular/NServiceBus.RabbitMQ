﻿namespace NServiceBus.Transports.RabbitMQ.Routing
{
    using System;
    using global::RabbitMQ.Client;

    /// <summary>
    /// Route using a static routing convention for routing messages from publishers to subscribers using routing keys
    /// </summary>
    class DirectRoutingTopology : IRoutingTopology
    {
        public DirectRoutingTopology(Conventions conventions, bool useDurableExchanges)
        {
            this.conventions = conventions;
            this.useDurableExchanges = useDurableExchanges;
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
            channel.BasicPublish(ExchangeName(), GetRoutingKeyForPublish(type), true, properties, message.Body);
        }

        public void Send(IModel channel, string address, OutgoingMessage message, IBasicProperties properties)
        {
            channel.BasicPublish(string.Empty, address, true, properties, message.Body);
        }

        
        public void RawSendInCaseOfFailure(IModel channel, string address, byte[] body, IBasicProperties properties)
        {
            channel.BasicPublish(string.Empty, address, true, properties, body);
        }

        public void Initialize(IModel channel, string main)
        {
            //nothing needs to be done for direct routing
        }

        string ExchangeName()
        {
            return conventions.ExchangeName(null, null);
        }

        void CreateExchange(IModel channel, string exchangeName)
        {
            if (exchangeName == AmqpTopicExchange)
                return;
            try
            {
                channel.ExchangeDeclare(exchangeName, ExchangeType.Topic, useDurableExchanges);
            }
                // ReSharper disable EmptyGeneralCatchClause
            catch (Exception)
                // ReSharper restore EmptyGeneralCatchClause
            {

            }
        }

        string GetRoutingKeyForPublish(Type eventType)
        {
            return conventions.RoutingKey(eventType);
        }

        string GetRoutingKeyForBinding(Type eventType)
        {
            if (eventType == typeof(IEvent) || eventType == typeof(object))
                return "#";


            return conventions.RoutingKey(eventType) + ".#";
        }

        const string AmqpTopicExchange = "amq.topic";

        readonly Conventions conventions;
        readonly bool useDurableExchanges;

        public class Conventions
        {
            public Conventions(Func<string, Type, string> exchangeName, Func<Type, string> routingKey)
            {
                ExchangeName = exchangeName;
                RoutingKey = routingKey;
            }

            public Func<string, Type, string> ExchangeName { get; }

            public Func<Type, string> RoutingKey { get; }
        }
    }
}