#pragma warning disable 1591

namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using Configuration.AdvancedExtensibility;

    [ObsoleteEx(
        Message = "The timeout manager has been removed, so it is no longer possible to consume legacy delayed messages from timeout storage.",
        TreatAsErrorFromVersion = "7",
        RemoveInVersion = "8")]
    public class DelayedDeliverySettings : ExposeSettings
    {
        DelayedDeliverySettings() : base(null) => throw new NotImplementedException();

        public DelayedDeliverySettings EnableTimeoutManager() => throw new NotImplementedException();
    }
}

namespace NServiceBus
{
    using System;
    using Transport.RabbitMQ;

    partial class RabbitMQTransportSettingsExtensions
    {
        [ObsoleteEx(
            Message = "The timeout manager has been removed, so there are no delayed delivery configuration options now.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static DelayedDeliverySettings DelayedDelivery(this TransportExtensions<RabbitMQTransport> transportExtensions)
        {
            throw new NotImplementedException();
        }
    }
}

#pragma warning restore 1591