#pragma warning disable 1591

namespace NServiceBus
{
    using System;
    using NServiceBus.Transport;

    public partial class RabbitMQTransport
    {
        [ObsoleteEx(
            Message = "Inject the ITransportAddressResolver type to access the address translation mechanism at runtime. See the NServiceBus version 8 upgrade guide for further details.",
            TreatAsErrorFromVersion = "9",
            RemoveInVersion = "10")]
#pragma warning disable CS0672 // Member overrides obsolete member
        public override string ToTransportAddress(QueueAddress address) => throw new NotImplementedException();
#pragma warning restore CS0672 // Member overrides obsolete member
    }

}

#pragma warning restore 1591
