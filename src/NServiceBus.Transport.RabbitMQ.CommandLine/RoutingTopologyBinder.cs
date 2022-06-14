namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System.CommandLine;
    using System.CommandLine.Binding;

    class RoutingTopologyBinder : BinderBase<IRoutingTopology>
    {
        public RoutingTopologyBinder(Option<RoutingTopologyType> routingTopologyTypeOption, Option<bool> useDurableEntitiesOption, Option<QueueType> queueTypeOption)
        {
            this.routingTopologyTypeOption = routingTopologyTypeOption;
            this.useDurableEntitiesOption = useDurableEntitiesOption;
            this.queueTypeOption = queueTypeOption;
        }

        protected override IRoutingTopology GetBoundValue(BindingContext bindingContext)
        {
            var routingTopologyType = bindingContext.ParseResult.GetValueForOption(routingTopologyTypeOption);
            var useDurableEntities = bindingContext.ParseResult.GetValueForOption(useDurableEntitiesOption);
            var queueType = bindingContext.ParseResult.GetValueForOption(queueTypeOption);

            return routingTopologyType switch
            {
                RoutingTopologyType.Conventional => new ConventionalRoutingTopology(useDurableEntities, queueType),
                RoutingTopologyType.Direct => new DirectRoutingTopology(new DirectRoutingTopology.Conventions(() => "amq.topic", DefaultRoutingKeyConvention.GenerateRoutingKey), useDurableEntities, queueType),
                _ => throw new InvalidOperationException()
            };
        }

        readonly Option<RoutingTopologyType> routingTopologyTypeOption;
        readonly Option<bool> useDurableEntitiesOption;
        readonly Option<QueueType> queueTypeOption;
    }
}
