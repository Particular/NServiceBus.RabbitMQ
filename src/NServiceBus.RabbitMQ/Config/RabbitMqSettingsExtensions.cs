namespace NServiceBus
{
    using System;
    using Configuration.AdvanceExtensibility;
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
        /// <param name="transportExtensions"></param>
        /// <param name="routingKeyConvention">The routing key conventions.</param>
        /// <param name="exchangeNameConvention">The exchange name convention.</param>
        public static TransportExtensions<RabbitMQTransport> UseDirectRoutingTopology(this TransportExtensions<RabbitMQTransport> transportExtensions, Func<Type, string> routingKeyConvention = null, Func<Address, Type, string> exchangeNameConvention = null)
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

            transportExtensions.GetSettings().Set<IRoutingTopology>(router);

            return transportExtensions;
        }

        /// <summary>
        /// Register a custom routing topology
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static TransportExtensions<RabbitMQTransport> UseRoutingTopology<T>(this TransportExtensions<RabbitMQTransport> transportExtensions) where T : IRoutingTopology
        {
            transportExtensions.GetSettings().Set<IRoutingTopology>(Activator.CreateInstance<T>());
            return transportExtensions;
        }

        /// <summary>
        /// Registers a custom connection manager te be used
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static TransportExtensions<RabbitMQTransport> UseConnectionManager<T>(this TransportExtensions<RabbitMQTransport> transportExtensions) where T : IManageRabbitMqConnections
        {
            transportExtensions.GetSettings().Set("IManageRabbitMqConnections", typeof(T));
            return transportExtensions;
        }

        /// <summary>
        /// Disables the separate receiver that pulls messages from the machine specific callback queue
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <returns></returns>
        public static TransportExtensions<RabbitMQTransport> DisableCallbackReceiver(this TransportExtensions<RabbitMQTransport> transportExtensions) 
        {
            transportExtensions.GetSettings().Set("RabbitMQ.UseCallbackReceiver", false);
            return transportExtensions;
        }

        /// <summary>
        /// Changes the number of threads that should be used for the callback receiver. The default is 1
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="maxConcurrency">The new value for concurrency</param>
        /// <returns></returns>
        public static TransportExtensions<RabbitMQTransport> CallbackReceiverMaxConcurrency(this TransportExtensions<RabbitMQTransport> transportExtensions, int maxConcurrency)
        {
            transportExtensions.GetSettings().Set("RabbitMQ.MaxConcurrencyForCallbackReceiver", maxConcurrency);
            return transportExtensions;
        }
    }
}