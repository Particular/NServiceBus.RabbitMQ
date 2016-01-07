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
            var callbacks = new Callbacks(context.Settings);

            context.Pipeline.Register<ReadIncomingCallbackAddressBehavior.Registration>();
            context.Pipeline.Register("SetOutgoingCallbackAddressBehavior", builder => new SetOutgoingCallbackAddressBehavior(callbacks), "Writes out callback address to in outgoing message.");
        }
    }
}