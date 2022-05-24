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
        /// <param name="useDurableEntities"></param>
        /// <returns></returns>
        public static RoutingTopology Conventional(bool useDurableEntities = true)
        {
            return new RoutingTopology(() => new ConventionalRoutingTopology(useDurableEntities));
        }

        internal static RoutingTopology Conventional(Func<Type, string> exchangeNameConvention, bool useDurable = true)
        {
            return new RoutingTopology(() => new ConventionalRoutingTopology(useDurable, exchangeNameConvention));
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="useDurableEntities"></param>
        /// <param name="exchangeNameConvention"></param>
        /// <param name="routingKeyConvention"></param>
        /// <returns></returns>
        public static RoutingTopology Direct(bool useDurableEntities = true, Func<string> exchangeNameConvention = null, Func<Type, string> routingKeyConvention = null)
        {
            return new RoutingTopology(() => new DirectRoutingTopology(useDurableEntities, exchangeNameConvention, routingKeyConvention));
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
