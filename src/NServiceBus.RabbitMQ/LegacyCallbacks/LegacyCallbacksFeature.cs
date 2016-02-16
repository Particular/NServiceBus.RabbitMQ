namespace NServiceBus
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
            context.Pipeline.Register("ReadIncomingLegacyCallbackAddressBehavior", new ReadIncomingLegacyCallbackAddressBehavior(), "Reads the legacy callback header from the incoming message.");
            context.Pipeline.Register("OverrideOutgoingReplyAddressIfLegacyCallbackAddressExistsBehavior", new OverrideOutgoingReplyAddressIfLegacyCallbackAddressExistsBehavior(), "Overrides the destination of replies if the legacy callback header has been provided.");
        }
    }
}