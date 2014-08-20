namespace NServiceBus
{
    using Configuration.AdvanceExtensibility;
    using Features;
    using Transports;

    /// <summary>
    /// Transport definition for RabbirtMQ
    /// </summary>
    public class RabbitMQTransport : TransportDefinition
    {
        /// <summary>
        /// Ctor
        /// </summary>
        public RabbitMQTransport()
        {
            HasNativePubSubSupport = true;
            HasSupportForCentralizedPubSub = true;
            HasSupportForDistributedTransactions = false;
        }

        /// <summary>
        /// Gives implementations access to the <see cref="T:NServiceBus.ConfigurationBuilder"/> instance at configuration time.
        /// </summary>
        protected override void Configure(ConfigurationBuilder config)
        {
            config.EnableFeature<RabbitMqTransport>();
            config.EnableFeature<TimeoutManagerBasedDeferral>();

            config.GetSettings().EnableFeatureByDefault<TimeoutManager>();

            //enable the outbox unless the users hasn't disabled it
            if (config.GetSettings().GetOrDefault<bool>(typeof(Features.Outbox).FullName))
            {
                config.EnableOutbox();
            }
        }
    }
}