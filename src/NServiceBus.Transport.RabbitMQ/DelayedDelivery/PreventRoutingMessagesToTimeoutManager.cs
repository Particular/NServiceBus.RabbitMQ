namespace NServiceBus.Transport.RabbitMQ
{
    using Features;

    class PreventRoutingMessagesToTimeoutManager : Feature
    {
        public PreventRoutingMessagesToTimeoutManager()
        {
            EnableByDefault();

            Prerequisite(context => !context.Settings.HasSetting(SettingsKeys.DisableTimeoutManager), "The timeout manager is disabled.");
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var routingTopologySupportsDelayedDelivery = (bool)context.Settings.Get(SettingsKeys.RoutingTopologySupportsDelayedDelivery);

            if (routingTopologySupportsDelayedDelivery)
            {
                context.Pipeline.Remove("RouteDeferredMessageToTimeoutManager");
            }
        }
    }
}
