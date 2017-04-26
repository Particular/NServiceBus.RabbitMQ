#pragma warning disable 1591

namespace NServiceBus
{
    using System;

    public static partial class RabbitMQTransportSettingsExtensions
    {
        [ObsoleteEx(Message = "Replaced by NServiceBus.Callbacks package", RemoveInVersion = "5.0", TreatAsErrorFromVersion = "4.0")]
        public static TransportExtensions<RabbitMQTransport> DisableCallbackReceiver(this TransportExtensions<RabbitMQTransport> transportExtensions)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(Message = "Replaced by NServiceBus.Callbacks package", RemoveInVersion = "5.0", TreatAsErrorFromVersion = "4.0")]
        public static TransportExtensions<RabbitMQTransport> CallbackReceiverMaxConcurrency(this TransportExtensions<RabbitMQTransport> transportExtensions, int maxConcurrency)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(RemoveInVersion = "5.0", TreatAsErrorFromVersion = "4.0")]
        public static TransportExtensions<RabbitMQTransport> UseConnectionManager<T>(this TransportExtensions<RabbitMQTransport> transportExtensions)
        {
            throw new NotImplementedException();
        }
    }
}

namespace NServiceBus.Transport.RabbitMQ
{
    public partial class DelayedDeliverySettings
    {
        [ObsoleteEx(RemoveInVersion = "5.0", TreatAsErrorFromVersion = "5.0")]
        public DelayedDeliverySettings AllEndpointsSupportDelayedDelivery()
        {
            return this;
        }
    }
}

namespace NServiceBus.Transports.RabbitMQ
{
    [ObsoleteEx(RemoveInVersion = "5.0", TreatAsErrorFromVersion = "4.0")]
    public interface IManageRabbitMqConnections
    {
    }
}

namespace NServiceBus.Transports.RabbitMQ.Routing
{
    [ObsoleteEx(ReplacementTypeOrMember = "NServiceBus.Transport.RabbitMQ.IRoutingTopology", RemoveInVersion = "5.0", TreatAsErrorFromVersion = "4.0")]
    public interface IRoutingTopology
    {
    }
}

#pragma warning restore 1591
