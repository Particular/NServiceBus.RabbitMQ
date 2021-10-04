#pragma warning disable 1591
#pragma warning disable 618

namespace NServiceBus
{
    using System;
    using Transport.RabbitMQ;

    public static partial class RabbitMQTransportSettingsExtensions
    {
        [ObsoleteEx(
    Message = "Using custom topologies is not possible with the legacy API. A custom topology can be provided using by creating a new instance of the RabbitMqTransport class.",
    TreatAsErrorFromVersion = "7",
    RemoveInVersion = "8")]
        public static TransportExtensions<RabbitMQTransport> UseCustomRoutingTopology(
    this TransportExtensions<RabbitMQTransport> transport,
    Func<bool, IRoutingTopology> topologyFactory)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            Message = "Disabling the durable exchanges is not possible in the legacy API. Use the new API and create the topology instance with appropriate arguments. See the upgrade guide for further details.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static TransportExtensions<RabbitMQTransport> DisableDurableExchangesAndQueues(this TransportExtensions<RabbitMQTransport> transport)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            Message = "The TimeoutManager has been removed from NServiceBus 8. See the upgrade guide for details on how to use the timeout migration tool.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static void DelayedDelivery(this TransportExtensions<RabbitMQTransport> transport)
        {
            throw new NotImplementedException();
        }
    }

    public partial class RabbitMQTransport
    {
        internal string LegacyApiConnectionString { get; set; }
        bool legacyMode;

        internal RabbitMQTransport()
            : base(TransportTransactionMode.ReceiveOnly, true, true, true)
        {
            legacyMode = true;
        }

        void ValidateAndApplyLegacyConfiguration()
        {
            if (!legacyMode)
            {
                return;
            }

            if (RoutingTopology == null)
            {
                throw new Exception("A routing topology must be configured with one of the 'EndpointConfiguration.UseTransport<RabbitMQTransport>().UseXXXXRoutingTopology()` methods.");
            }

            if (string.IsNullOrEmpty(LegacyApiConnectionString))
            {
                throw new Exception("A connection string must be configured with 'EndpointConfiguration.UseTransport<RabbitMQTransport>().ConnectionString()` method.");
            }

            if (LegacyApiConnectionString.StartsWith("amqp", StringComparison.OrdinalIgnoreCase))
            {
                AmqpConnectionString.Parse(LegacyApiConnectionString)(this);
            }
            else
            {
                NServiceBusConnectionString.Parse(LegacyApiConnectionString)(this);
            }
        }
    }
}
#pragma warning restore 618
#pragma warning restore 1591
