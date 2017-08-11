#pragma warning disable 1591

namespace NServiceBus
{
    using System;
    using Transport.RabbitMQ;

    public static partial class RabbitMQTransportSettingsExtensions
    {
        [ObsoleteEx(RemoveInVersion = "6.0", TreatAsErrorFromVersion = "5.0", ReplacementTypeOrMember = "RabbitMQTransportSettingsExtensions.UseRoutingTopology(TransportExtensions<RabbitMQTransport> transportExtensions, Func<bool, IRoutingTopology>)")]
        public static TransportExtensions<RabbitMQTransport> UseRoutingTopology<T>(this TransportExtensions<RabbitMQTransport> transportExtensions) where T : IRoutingTopology, new()
        {
            throw new NotImplementedException();
        }
    }
}

#pragma warning restore 1591