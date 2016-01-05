namespace NServiceBus
{
    using NServiceBus.Features;
    using NServiceBus.Transports;
    using NServiceBus.Transports.RabbitMQ;

    class RabbitMQTransportCallbacks : Feature
    {
        public RabbitMQTransportCallbacks()
        {
            EnableByDefault();
            Prerequisite(c =>
            {
                var transportDefinition = c.Settings.Get<TransportDefinition>();
                return transportDefinition is RabbitMQTransport;
            }, "Checks if Rabbit is the selected transport");
        }

        /// <summary>
        /// Called when the features is activated.
        /// </summary>
        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Pipeline.Register<ReadIncomingCallbackAddressBehavior.Registration>();
            context.Pipeline.Register<SetOutgoingCallbackAddressBehavior.Registration>();
        }
    }
}