using System;

#pragma warning disable 1591

namespace NServiceBus
{
#pragma warning disable 618
    public static class RabbitMQTransportSettingsExtensions
    {
        [ObsoleteEx(
            ReplacementTypeOrMember = "RabbitMQTransport.MessageIdStrategy",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static TransportExtensions<RabbitMQTransport> CustomMessageIdStrategy(
            this TransportExtensions<RabbitMQTransport> transportExtensions,
            Func<RabbitMQ.Client.Events.BasicDeliverEventArgs, string> customIdStrategy)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            Message = "The configuration has been moved to the topology implementations.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static TransportExtensions<RabbitMQTransport> DisableDurableExchangesAndQueues(
            this TransportExtensions<RabbitMQTransport> transportExtensions)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "RabbitMQTransport.ValidateRemoteCertificate",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static TransportExtensions<RabbitMQTransport> DisableRemoteCertificateValidation(
            this TransportExtensions<RabbitMQTransport> transportExtensions)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "RabbitMQTransport.PrefetchCountCalculation",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static TransportExtensions<RabbitMQTransport> PrefetchCount(
            this TransportExtensions<RabbitMQTransport> transportExtensions, ushort prefetchCount)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "RabbitMQTransport.PrefetchCountCalculation",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static TransportExtensions<RabbitMQTransport> PrefetchMultiplier(
            this TransportExtensions<RabbitMQTransport> transportExtensions, int prefetchMultiplier)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "RabbitMQTransport.ClientCertificate",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static TransportExtensions<RabbitMQTransport> SetClientCertificate(
            this TransportExtensions<RabbitMQTransport> transportExtensions,
            System.Security.Cryptography.X509Certificates.X509Certificate2 clientCertificate)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "RabbitMQTransport.ClientCertificate",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static TransportExtensions<RabbitMQTransport> SetClientCertificate(
            this TransportExtensions<RabbitMQTransport> transportExtensions, string path, string password)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "RabbitMQTransport.HeartbeatInterval",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static TransportExtensions<RabbitMQTransport> SetHeartbeatInterval(
            this TransportExtensions<RabbitMQTransport> transportExtensions, TimeSpan heartbeatInterval)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "RabbitMQTransport.NetworkRecoveryInterval",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static TransportExtensions<RabbitMQTransport> SetNetworkRecoveryInterval(
            this TransportExtensions<RabbitMQTransport> transportExtensions, TimeSpan networkRecoveryInterval)
        {
            throw new NotImplementedException();
        }


        [ObsoleteEx(
            ReplacementTypeOrMember = "RabbitMQTransport.TimeToWaitBeforeTriggeringCircuitBreaker",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static TransportExtensions<RabbitMQTransport> TimeToWaitBeforeTriggeringCircuitBreaker(
            this TransportExtensions<RabbitMQTransport> transportExtensions, TimeSpan waitTime)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "RabbitMQTransport.RoutingTopology",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static TransportExtensions<RabbitMQTransport> UseConventionalRoutingTopology(
            this TransportExtensions<RabbitMQTransport> transportExtensions)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "RabbitMQTransport.RoutingTopology",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static TransportExtensions<RabbitMQTransport> UseCustomRoutingTopology(
            this TransportExtensions<RabbitMQTransport> transportExtensions,
            Func<bool, NServiceBus.Transport.RabbitMQ.IRoutingTopology> topologyFactory)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "RabbitMQTransport.RoutingTopology",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static TransportExtensions<RabbitMQTransport> UseDirectRoutingTopology(
            this TransportExtensions<RabbitMQTransport> transportExtensions,
            Func<Type, string> routingKeyConvention = null, Func<string> exchangeNameConvention = null)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "RabbitMQTransport.UseExternalAuthMechanism",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static TransportExtensions<RabbitMQTransport> UseExternalAuthMechanism(
            this TransportExtensions<RabbitMQTransport> transportExtensions)
        {
            throw new NotImplementedException();
        }
    }
#pragma warning restore 618
}

#pragma warning restore 1591