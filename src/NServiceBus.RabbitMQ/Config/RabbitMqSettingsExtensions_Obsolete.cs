namespace NServiceBus
{
    using System;
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
        /// <param name="transportConfiguration"></param>
        /// <param name="routingKeyConvention"></param>
        /// <param name="exchangeNameConvention"></param>
        [ObsoleteEx(Replacement = "Configure.With(c => c.UseTransport<RabbitMQTransport>().UseDirectRoutingTopology(routingKeyConvention, exchangeNameConvention)", RemoveInVersion = "3.0", TreatAsErrorFromVersion = "2.0")]
// ReSharper disable UnusedParameter.Global
        public static void UseDirectRoutingTopology(this TransportConfiguration transportConfiguration, Func<Type, string> routingKeyConvention = null, Func<Address, Type, string> exchangeNameConvention = null)
// ReSharper restore UnusedParameter.Global
        {
            throw new InvalidOperationException();            
        }

        /// <summary>
        /// Register a custom routing topology
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [ObsoleteEx(Replacement = "Configure.With(c => c.UseTransport<RabbitMQTransport>().UseRoutingTopology<T>()", RemoveInVersion = "3.0", TreatAsErrorFromVersion = "2.0")]
// ReSharper disable UnusedParameter.Global
        public static void UseRoutingTopology<T>(this TransportConfiguration transportConfiguration) where T : IRoutingTopology
// ReSharper restore UnusedParameter.Global
        {
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Registers a custom connection manager te be used
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [ObsoleteEx(Replacement = "Configure.With(c => c.UseTransport<RabbitMQTransport>().UseConnectionManager<T>()", RemoveInVersion = "3.0", TreatAsErrorFromVersion = "2.0")]
// ReSharper disable UnusedParameter.Global
        public static void UseConnectionManager<T>(this TransportConfiguration transportConfiguration) where T : IManageRabbitMqConnections
// ReSharper restore UnusedParameter.Global
        {
            throw new InvalidOperationException();
        }
    }
}