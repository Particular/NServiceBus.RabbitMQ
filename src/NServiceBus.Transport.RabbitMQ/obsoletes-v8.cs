#pragma warning disable 1591

namespace NServiceBus
{
    using System;

    public static partial class RabbitMQTransportSettingsExtensions
    {
        [ObsoleteEx(
            Message = "Choosing a queue type is mandatory now.",
            ReplacementTypeOrMember = "UseConventionalRoutingTopology(QueueType queueType)",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static TransportExtensions<RabbitMQTransport> UseConventionalRoutingTopology(this TransportExtensions<RabbitMQTransport> transportExtensions)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            Message = "Choosing a queue type is mandatory now.",
            ReplacementTypeOrMember = "UseDirectRoutingTopology(QueueType queueType, Func<Type, string> routingKeyConvention, Func<string> exchangeNameConvention)",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static TransportExtensions<RabbitMQTransport> UseDirectRoutingTopology(this TransportExtensions<RabbitMQTransport> transportExtensions, Func<Type, string> routingKeyConvention = null, Func<string> exchangeNameConvention = null)
        {
            throw new NotImplementedException();
        }
    }
}
#pragma warning restore 1591
