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

        protected override void Setup(FeatureConfigurationContext context) =>
            context.Pipeline.Remove("RouteDeferredMessageToTimeoutManager");
    }
}
