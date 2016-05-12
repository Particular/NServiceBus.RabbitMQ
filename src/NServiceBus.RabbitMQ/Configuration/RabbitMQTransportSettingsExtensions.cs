namespace NServiceBus
{
    using System;
    using Configuration.AdvanceExtensibility;
    using RabbitMQ.Client.Events;
    using NServiceBus.Transport.RabbitMQ;

    /// <summary>
    /// Adds access to the RabbitMQ transport config to the global Transports object.
    /// </summary>
    public static partial class RabbitMQTransportSettingsExtensions
    {
        /// <summary>
        /// Uses the direct routing topology with the specified conventions.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="routingKeyConvention">The routing key convention.</param>
        /// <param name="exchangeNameConvention">The exchange name convention.</param>
        public static TransportExtensions<RabbitMQTransport> UseDirectRoutingTopology(this TransportExtensions<RabbitMQTransport> transportExtensions, Func<Type, string> routingKeyConvention = null, Func<string, Type, string> exchangeNameConvention = null)
        {
            if (routingKeyConvention == null)
            {
                routingKeyConvention = DefaultRoutingKeyConvention.GenerateRoutingKey;
            }

            if (exchangeNameConvention == null)
            {
                exchangeNameConvention = (address, eventType) => "amq.topic";
            }

            transportExtensions.GetSettings().Set<DirectRoutingTopology.Conventions>(new DirectRoutingTopology.Conventions(exchangeNameConvention, routingKeyConvention));

            return transportExtensions;
        }

        /// <summary>
        /// Registers a custom routing topology.
        /// </summary>
        public static TransportExtensions<RabbitMQTransport> UseRoutingTopology<T>(this TransportExtensions<RabbitMQTransport> transportExtensions) where T : IRoutingTopology
        {
            transportExtensions.GetSettings().Set<IRoutingTopology>(Activator.CreateInstance<T>());
            return transportExtensions;
        }

        /// <summary>
        /// Allows the user to control how the message id is determined. Mostly useful when doing native integration with non NSB endpoints.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="customIdStrategy">The user defined strategy for giving the message a unique id.</param>
        /// <returns></returns>
        public static TransportExtensions<RabbitMQTransport> CustomMessageIdStrategy(this TransportExtensions<RabbitMQTransport> transportExtensions, Func<BasicDeliverEventArgs, string> customIdStrategy)
        {
            transportExtensions.GetSettings().Set(SettingsKeys.CustomMessageIdStrategy, customIdStrategy);
            return transportExtensions;
        }

        /// <summary>
        /// Overrides the default time to wait before triggering a circuit breaker that initiates the endpoint shutdown procedure when the message pump's connection to the broker is lost and cannot be recovered.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="waitTime">The time to wait before triggering the circuit breaker.</param>
        public static TransportExtensions<RabbitMQTransport> TimeToWaitBeforeTriggeringCircuitBreaker(this TransportExtensions<RabbitMQTransport> transportExtensions, TimeSpan waitTime)
        {
            transportExtensions.GetSettings().Set(SettingsKeys.TimeToWaitBeforeTriggeringCircuitBreaker, waitTime);
            return transportExtensions;
        }
    }
}