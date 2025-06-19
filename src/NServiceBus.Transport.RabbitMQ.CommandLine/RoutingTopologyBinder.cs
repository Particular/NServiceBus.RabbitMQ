namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System.CommandLine;

    class RoutingTopologyBinder(Option<RoutingTopologyType> routingTopologyTypeOption, Option<bool> useDurableEntitiesOption, Option<QueueType> queueTypeOption)
    {
        public IRoutingTopology CreateRoutingTopology(ParseResult parseResult)
        {
            var routingTopologyType = parseResult.GetValue(routingTopologyTypeOption);
            var useDurableEntities = parseResult.GetValue(useDurableEntitiesOption);
            var queueType = parseResult.GetValue(queueTypeOption);

            return routingTopologyType switch
            {
                RoutingTopologyType.Conventional => new ConventionalRoutingTopology(useDurableEntities, queueType),
                RoutingTopologyType.Direct => new DirectRoutingTopology(useDurableEntities, queueType),
                _ => throw new InvalidOperationException()
            };
        }
    }
}
