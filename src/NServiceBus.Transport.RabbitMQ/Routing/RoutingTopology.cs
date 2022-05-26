#nullable disable
namespace NServiceBus
{
    using System;
    using NServiceBus.Transport.RabbitMQ;

    /// <summary>
    ///
    /// </summary>
    public class RoutingTopology
    {
        readonly Func<IRoutingTopology> routingTopology;

        RoutingTopology(Func<IRoutingTopology> routingTopology)
        {
            this.routingTopology = routingTopology;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="queueType"></param>
        /// <param name="useDurableEntities"></param>
        /// <returns></returns>
        public static RoutingTopology Conventional(QueueType queueType, bool useDurableEntities = true)
        {
            return new RoutingTopology(() => new ConventionalRoutingTopology(useDurableEntities, queueType));
        }

        internal static RoutingTopology Conventional(QueueType queueType, Func<Type, string> exchangeNameConvention, bool useDurable = true)
        {
            return new RoutingTopology(() => new ConventionalRoutingTopology(useDurable, queueType, exchangeNameConvention));
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="queueType"></param>
        /// <param name="useDurableEntities"></param>
        /// <param name="exchangeNameConvention"></param>
        /// <param name="routingKeyConvention"></param>
        /// <returns></returns>
        public static RoutingTopology Direct(QueueType queueType, bool useDurableEntities = true, Func<string> exchangeNameConvention = null, Func<Type, string> routingKeyConvention = null)
        {
            return new RoutingTopology(() => new DirectRoutingTopology(useDurableEntities, queueType, exchangeNameConvention, routingKeyConvention));
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="routingTopology"></param>
        /// <returns></returns>
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
