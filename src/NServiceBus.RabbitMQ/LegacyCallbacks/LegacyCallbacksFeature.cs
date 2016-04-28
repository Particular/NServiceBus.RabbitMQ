namespace NServiceBus.Transport.RabbitMQ
{
    using NServiceBus.Features;

    class LegacyCallbacksFeature : Feature
    {
        public LegacyCallbacksFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Pipeline.Register("OverrideOutgoingReplyAddressIfLegacyCallbackAddressExistsBehavior", new OverrideOutgoingReplyAddressIfLegacyCallbackAddressExistsBehavior(), "Overrides the destination of replies if the legacy callback header has been provided.");
        }
    }
}