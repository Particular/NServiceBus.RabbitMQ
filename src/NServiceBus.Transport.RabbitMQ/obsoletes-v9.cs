#pragma warning disable 1591

namespace NServiceBus
{
    using System;
    using Transport.RabbitMQ;

    public static partial class RabbitMQTransportSettingsExtensions
    {
        [ObsoleteEx(
            Message = "The transport no longer has configurable settings for delayed delivery.",
            TreatAsErrorFromVersion = "8",
            RemoveInVersion = "9")]
        public static DelayedDeliverySettings DelayedDelivery(this TransportExtensions<RabbitMQTransport> transportExtensions)
        {
            throw new NotImplementedException();
        }
    }

    [ObsoleteEx(
        Message = "The transport no longer has configurable settings for delayed delivery.",
        TreatAsErrorFromVersion = "8",
        RemoveInVersion = "9")]
    public class DelayedDeliverySettings
    {
        [ObsoleteEx(
            Message = "The TimeoutManager has been removed from NServiceBus 8. See the upgrade guide for details on how to use the timeout migration tool.",
            TreatAsErrorFromVersion = "8",
            RemoveInVersion = "9")]
        public DelayedDeliverySettings EnableTimeoutManager()
        {
            throw new NotImplementedException();
        }
    }

    public partial class RabbitMQTransport
    {
        internal string LegacyApiConnectionString { get; set; }

        internal Func<bool, IRoutingTopology> TopologyFactory { get; set; }

        internal bool UseDurableExchangesAndQueues { get; set; } = true;

        bool legacyMode;

        internal RabbitMQTransport() : base(TransportTransactionMode.ReceiveOnly, true, true, true)
        {
            legacyMode = true;
        }

        void ValidateAndApplyLegacyConfiguration()
        {
            if (!legacyMode)
            {
                return;
            }

            if (TopologyFactory == null)
            {
                throw new Exception("A routing topology must be configured with one of the 'EndpointConfiguration.UseTransport<RabbitMQTransport>().UseXXXXRoutingTopology()` methods. Most new projects should use the Conventional routing topology.");
            }

            RoutingTopology = TopologyFactory(UseDurableExchangesAndQueues);

            if (string.IsNullOrEmpty(LegacyApiConnectionString))
            {
                throw new Exception("A connection string must be configured with 'EndpointConfiguration.UseTransport<RabbitMQTransport>().ConnectionString()` method.");
            }

            ConnectionConfiguration = ConnectionConfiguration.Create(LegacyApiConnectionString);
        }
    }
}

#pragma warning restore 1591
