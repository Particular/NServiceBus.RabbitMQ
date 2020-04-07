#pragma warning disable 1591

namespace NServiceBus
{
    using System;
    using Transport.RabbitMQ;

    public static partial class RabbitMQTransportSettingsExtensions
    {
        [ObsoleteEx(RemoveInVersion = "6.0", TreatAsErrorFromVersion = "5.0", ReplacementTypeOrMember = "RabbitMQTransportSettingsExtensions.UseCustomRoutingTopology(TransportExtensions<RabbitMQTransport> transportExtensions, Func<bool, IRoutingTopology>)")]
        public static TransportExtensions<RabbitMQTransport> UseRoutingTopology<T>(this TransportExtensions<RabbitMQTransport> transportExtensions) where T : IRoutingTopology, new()
        {
            throw new NotImplementedException();
        }
    }
}

namespace NServiceBus.Transport.RabbitMQ
{
    using System;

    public partial class DelayedDeliverySettings
    {
        [ObsoleteEx(RemoveInVersion = "6.0", TreatAsErrorFromVersion = "5.0", Message = "The timeout manager is now disabled by default.")]
        public DelayedDeliverySettings DisableTimeoutManager()
        {
            throw new NotImplementedException();
        }
    }
}

#pragma warning restore 1591