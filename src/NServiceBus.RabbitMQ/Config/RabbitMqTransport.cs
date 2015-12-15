namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Text;
    using Configuration.AdvanceExtensibility;
    using Features;
    using NServiceBus.Performance.TimeToBeReceived;
    using NServiceBus.Settings;
    using NServiceBus.Transports.RabbitMQ;
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
            RequireOutboxConsent = false;
            //HasNativePubSubSupport = true;
            //HasSupportForCentralizedPubSub = true;
            //HasSupportForDistributedTransactions = false;
        }

        ///// <summary>
        ///// Gives implementations access to the <see cref="T:NServiceBus.BusConfiguration"/> instance at configuration time.
        ///// </summary>
        //protected override void Configure(BusConfiguration config)
        //{
        //    config.EnableFeature<RabbitMqTransportFeature>();
        //    config.EnableFeature<TimeoutManagerBasedDeferral>();

        //    config.GetSettings().EnableFeatureByDefault<TimeoutManager>();

        //    //enable the outbox unless the users hasn't disabled it
        //    if (config.GetSettings().GetOrDefault<bool>(typeof(Features.Outbox).FullName))
        //    {
        //        config.EnableOutbox();
        //    }
        //}

        /// <summary>
        /// Configures transport for receiving.
        /// </summary>
        protected override TransportReceivingConfigurationResult ConfigureForReceiving(TransportReceivingConfigurationContext context)
        {
        return new TransportReceivingConfigurationResult();
        }

        /// <summary>
        /// Configures transport for sending.
        /// </summary>
        protected override TransportSendingConfigurationResult ConfigureForSending(TransportSendingConfigurationContext context)
        {
            return new TransportSendingConfigurationResult(() => new RabbitMqMessageSender(), );
        }

        /// <summary>
        /// Returns the list of supported delivery constraints for this transport.
        /// </summary>
        public override IEnumerable<Type> GetSupportedDeliveryConstraints()
        {
             ???
        }

        /// <summary>
        /// Gets the highest supported transaction mode for the this transport.
        /// </summary>
        public override TransportTransactionMode GetSupportedTransactionMode()
        {
            return TransportTransactionMode.ReceiveOnly;
        }

        /// <summary>
        /// Will be called if the transport has indicated that it has native support for pub sub.
        ///             Creates a transport address for the input queue defined by a logical address.
        /// </summary>
        public override IManageSubscriptions GetSubscriptionManager()
        {
            return new RabbitMqSubscriptionManager();
        }

        /// <summary>
        /// Returns the discriminator for this endpoint instance.
        /// </summary>
        public override string GetDiscriminatorForThisEndpointInstance(ReadOnlySettings settings)
        {
            return null;
        }

        /// <summary>
        /// Converts a given logical address to the transport address.
        /// </summary>
        /// <param name="logicalAddress">The logical address.</param>
        /// <returns>
        /// The transport address.
        /// </returns>
        public override string ToTransportAddress(LogicalAddress logicalAddress)
        {
            var queue = new StringBuilder(logicalAddress.EndpointInstance.Endpoint.ToString());

            if (logicalAddress.EndpointInstance.UserDiscriminator != null)
            {
                queue.Append("-" + logicalAddress.EndpointInstance.UserDiscriminator);
            }
            if (logicalAddress.Qualifier != null)
            {
                queue.Append("." + logicalAddress.Qualifier);
            }

            return queue.ToString();
        }

        /// <summary>
        /// Returns the outbound routing policy selected for the transport.
        /// </summary>
        public override OutboundRoutingPolicy GetOutboundRoutingPolicy(ReadOnlySettings settings)
        {
            return new OutboundRoutingPolicy(OutboundRoutingType.Unicast, OutboundRoutingType.Multicast, OutboundRoutingType.Unicast);
        }

        /// <summary>
        /// Gets an example connection string to use when reporting lack of configured connection string to the user.
        /// </summary>
        public override string ExampleConnectionStringForErrorMessage => "host=localhost";
    }
}
