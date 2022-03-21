#pragma warning disable 1591

namespace NServiceBus
{
    using System;
    using Transport.RabbitMQ;

    public static partial class RabbitMQTransportSettingsExtensions
    {
        [ObsoleteEx(
            Message = "To use a custom topology, create a new instance of the RabbitMQTransport class and pass it into endpointConfiguration.UseTransport(rabbitMqTransportDefinition).",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static TransportExtensions<RabbitMQTransport> UseCustomRoutingTopology(
    this TransportExtensions<RabbitMQTransport> transport,
    Func<bool, IRoutingTopology> topologyFactory)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            Message = "In order to disable durable exchanges and queues, create a new instance of the RabbitMQTransport class and set the RoutingTopology property with an implementation that passes in the desired value for the useDurableEntities parameter, then pass the RabbitMQTransport instance to endpointConfiguration.UseTransport(rabbitMqTransportDefinition). See the upgrade guide for further details.",
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
                throw new Exception("A routing topology must be configured with one of the 'EndpointConfiguration.UseTransport<RabbitMQTransport>().UseXXXXRoutingTopology()` methods. Most new projects should use the Conventional routing topology.");
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
#pragma warning restore 1591
