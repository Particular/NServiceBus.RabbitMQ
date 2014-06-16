namespace NServiceBus
{
    using System;
    using Transports.RabbitMQ;
    using Transports.RabbitMQ.Routing;

    /// <summary>
    /// Adds access to the RabbitMQ transport config to the global Transports object
    /// </summary>
    public static class RabbitMqSettingsExtensions
    {
        /// <summary>
        /// Use the direct routing topology with the given conventions
        /// </summary>
        /// <param name="transportConfiguration"></param>
        /// <param name="routingKeyConvention"></param>
        /// <param name="exchangeNameConvention"></param>
        public static void UseDirectRoutingTopology(this TransportConfiguration transportConfiguration,Func<Type, string> routingKeyConvention = null, Func<Address, Type, string> exchangeNameConvention = null)
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

            transportConfiguration.Config.Settings.Set<IRoutingTopology>(router);
        }

        /// <summary>
        /// Register a custom routing topology
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static void UseRoutingTopology<T>(this TransportConfiguration transportConfiguration) where T : IRoutingTopology
        {
            transportConfiguration.Config.Settings.Set<IRoutingTopology>(Activator.CreateInstance<T>());
        }

        /// <summary>
        /// Registers a custom connection manager te be used
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static void UseConnectionManager<T>(this TransportConfiguration transportConfiguration) where T:IManageRabbitMqConnections
        {
            transportConfiguration.Config.Settings.Set("IManageRabbitMqConnections",typeof(T));
        }
    }
}