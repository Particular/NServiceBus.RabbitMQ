#pragma warning disable 1591

namespace NServiceBus
{
    using System;

    public static partial class RabbitMqSettingsExtensions
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
    }
}

#pragma warning restore 1591
