#nullable disable
namespace NServiceBus
{
    using System;
    using NServiceBus.Transport.RabbitMQ;

    /// <summary>
    /// Configures the transport to use the specified routing topology.
    /// </summary>
    public class RoutingTopology
    {
        readonly Func<IRoutingTopology> routingTopology;

        RoutingTopology(Func<IRoutingTopology> routingTopology)
        {
            this.routingTopology = routingTopology;
        }

        /// <summary>
        /// Configures the transport to use the conventional routing topology.
        /// </summary>
        /// <param name="queueType">The type of queue that the endpoint should use.</param>
        /// <param name="useDurableEntities">Specifies whether exchanges and queues should be declared as durable or not.</param>
        public static RoutingTopology Conventional(QueueType queueType, bool useDurableEntities = true)
        {
            return new RoutingTopology(() => new ConventionalRoutingTopology(useDurableEntities, queueType));
        }

        internal static RoutingTopology Conventional(QueueType queueType, Func<Type, string> exchangeNameConvention, bool useDurable = true)
        {
            return new RoutingTopology(() => new ConventionalRoutingTopology(useDurable, queueType, exchangeNameConvention));
        }

        /// <summary>
        /// Configures the transport to use the direct routing topology.
        /// </summary>
        /// <param name="queueType">The type of queue to use.</param>
        /// <param name="useDurableEntities">Specifies whether exchanges and queues should be declared as durable or not.</param>
        /// <param name="routingKeyConvention">The routing key convention.</param>
        /// <param name="exchangeNameConvention">The exchange name convention.</param>
        public static RoutingTopology Direct(QueueType queueType, bool useDurableEntities = true, Func<Type, string> routingKeyConvention = null, Func<string> exchangeNameConvention = null)
        {
            return new RoutingTopology(() => new DirectRoutingTopology(useDurableEntities, queueType, routingKeyConvention, exchangeNameConvention));
        }

        /// <summary>
        /// Configures the transport to use a custom routing topology.
        /// </summary>
        /// <param name="routingTopology">The custom routing topology instance to use.</param>
        public static RoutingTopology Custom(IRoutingTopology routingTopology)
        {
            return new RoutingTopology(() => routingTopology);
        }

        internal IRoutingTopology Create()
        {
            return routingTopology();
        }
    }
}
