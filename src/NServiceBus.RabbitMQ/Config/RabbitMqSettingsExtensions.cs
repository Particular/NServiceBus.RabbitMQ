namespace NServiceBus
{
    using System;
    using Configuration.AdvanceExtensibility;
    using Transports.RabbitMQ;
    using Transports.RabbitMQ.Routing;

    /// <summary>
    /// Adds access to the RabbitMQ transport config to the global Transports object
    /// </summary>
    public static partial class RabbitMqSettingsExtensions
    {
        /// <summary>
        /// Use the direct routing topology with the given conventions
        /// </summary>
        /// <param name="transportExtentions"></param>
        /// <param name="routingKeyConvention">The routing key conventions.</param>
        /// <param name="exchangeNameConvention">The exchange name convention.</param>
        public static TransportExtentions<RabbitMQTransport> UseDirectRoutingTopology(this TransportExtentions<RabbitMQTransport> transportExtentions, Func<Type, string> routingKeyConvention = null, Func<Address, Type, string> exchangeNameConvention = null)
        {
            if (routingKeyConvention == null)
            {
                routingKeyConvention = DefaultRoutingKeyConvention.GenerateRoutingKey;
            }

            if (exchangeNameConvention == null)
            {
                exchangeNameConvention = (address, eventType) => "amq.topic";
            }

            var router = new DirectRoutingTopology
            {
                ExchangeNameConvention = exchangeNameConvention,
                RoutingKeyConvention = routingKeyConvention
            };

            transportExtentions.GetSettings().Set<IRoutingTopology>(router);

            return transportExtentions;
        }

        /// <summary>
        /// Register a custom routing topology
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static TransportExtentions<RabbitMQTransport> UseRoutingTopology<T>(this TransportExtentions<RabbitMQTransport> transportExtentions) where T : IRoutingTopology
        {
            transportExtentions.GetSettings().Set<IRoutingTopology>(Activator.CreateInstance<T>());
            return transportExtentions;
        }

        /// <summary>
        /// Registers a custom connection manager te be used
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static TransportExtentions<RabbitMQTransport> UseConnectionManager<T>(this TransportExtentions<RabbitMQTransport> transportExtentions) where T : IManageRabbitMqConnections
        {
            transportExtentions.GetSettings().Set("IManageRabbitMqConnections", typeof(T));
            return transportExtentions;
        }
    }
}