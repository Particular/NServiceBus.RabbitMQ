#pragma warning disable 1591
#pragma warning disable 618

namespace NServiceBus
{
    using System;
    using System.Security.Cryptography.X509Certificates;
    using Transport.RabbitMQ;

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
            X509Certificate2 clientCertificate)
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
            Func<bool, IRoutingTopology> topologyFactory)
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

    public static class RabbitMqTransportApiExtensions
    {
        /// <summary>
        /// Configures NServiceBus to use the given transport.
        /// </summary>
        [ObsoleteEx(
            RemoveInVersion = "9",
            TreatAsErrorFromVersion = "8",
            ReplacementTypeOrMember = "EndpointConfiguration.UseTransport(TransportDefinition)")]
        public static RabbitMqTransportLegacySettings UseTransport<T>(this EndpointConfiguration config)
            where T : RabbitMQTransport
        {
            var transport = new RabbitMQTransport();

            var routing = config.UseTransport(transport);

            var settings = new RabbitMqTransportLegacySettings(transport, routing);

            return settings;
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

    /// <summary>
    /// RabbitMQ transport configuration settings.
    /// </summary>
    public class RabbitMqTransportLegacySettings : TransportSettings<RabbitMQTransport>
    {
        internal RabbitMQTransport RabbitMqTransport { get; } //for testing the shim

        internal RabbitMqTransportLegacySettings(RabbitMQTransport transport, RoutingSettings<RabbitMQTransport> routing)
            : base(transport, routing)

        {
            RabbitMqTransport = transport;
        }

        [ObsoleteEx(
            Message = "Using custom topologies is not possible with the legacy API. A custom topology can be provided using by creating a new instance of the RabbitMqTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public RabbitMqTransportLegacySettings UseCustomRoutingTopology(
            Func<bool, IRoutingTopology> topologyFactory)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            Message = "Disabling the durable exchanges is not possible in the legacy API. Use the new API and create the topology instance with appropriate arguments. See the upgrade guide for further details.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public RabbitMqTransportLegacySettings DisableDurableExchangesAndQueues()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// The connection string to use when connecting to the broker.
        /// </summary>
        [ObsoleteEx(
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "8",
            RemoveInVersion = "9")]
        public RabbitMqTransportLegacySettings ConnectionString(string connectionString)
        {
            Transport.LegacyApiConnectionString = connectionString;
            return this;
        }

        /// <summary>
        /// Allows the user to control how the message ID is determined. Mostly useful when doing native integration with non-NSB endpoints.
        /// </summary>
        /// <param name="customIdStrategy">The user-defined strategy for giving the message a unique ID.</param>
        [ObsoleteEx(
            ReplacementTypeOrMember = "RabbitMQTransport.MessageIdStrategy",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "8",
            RemoveInVersion = "9")]
        public RabbitMqTransportLegacySettings CustomMessageIdStrategy(
            Func<RabbitMQ.Client.Events.BasicDeliverEventArgs, string> customIdStrategy)
        {
            Transport.MessageIdStrategy = customIdStrategy;
            return this;
        }

        /// <summary>
        /// Disables all remote certificate validation when connecting to the broker via TLS.
        /// </summary>
        [ObsoleteEx(
            ReplacementTypeOrMember = "RabbitMQTransport.ValidateRemoteCertificate",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "8",
            RemoveInVersion = "9")]
        public RabbitMqTransportLegacySettings DisableRemoteCertificateValidation()
        {
            Transport.ValidateRemoteCertificate = false;
            return this;
        }

        /// <summary>
        /// Overrides the default prefetch count calculation with the specified value.
        /// </summary>
        /// <param name="prefetchCount">The prefetch count to use.</param>
        [ObsoleteEx(
            ReplacementTypeOrMember = "RabbitMQTransport.PrefetchCountCalculation",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "8",
            RemoveInVersion = "9")]
        public RabbitMqTransportLegacySettings PrefetchCount(ushort prefetchCount)
        {
            Transport.PrefetchCountCalculation = _ => prefetchCount;
            return this;
        }

        /// <summary>
        /// Specifies the multiplier to apply to the maximum concurrency value to calculate the prefetch count.
        /// </summary>
        /// <param name="prefetchMultiplier">The multiplier value to use in the prefetch calculation.</param>
        [ObsoleteEx(
            ReplacementTypeOrMember = "RabbitMQTransport.PrefetchCountCalculation",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "8",
            RemoveInVersion = "9")]
        public RabbitMqTransportLegacySettings PrefetchMultiplier(int prefetchMultiplier)
        {
            Transport.PrefetchCountCalculation = concurrency => prefetchMultiplier * concurrency;
            return this;
        }

        /// <summary>
        /// Specifies the certificate to use for client authentication when connecting to the broker via TLS.
        /// </summary>
        /// <param name="clientCertificate">The certificate to use for client authentication.</param>
        [ObsoleteEx(
            ReplacementTypeOrMember = "RabbitMQTransport.ClientCertificate",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "8",
            RemoveInVersion = "9")]
        public RabbitMqTransportLegacySettings SetClientCertificate(X509Certificate2 clientCertificate)
        {
            Transport.ClientCertificate = clientCertificate;
            return this;
        }

        /// <summary>
        /// Specifies the certificate to use for client authentication when connecting to the broker via TLS.
        /// </summary>
        /// <param name="path">The path to the certificate file.</param>
        /// <param name="password">The password for the certificate specified in <paramref name="path"/>.</param>
        [ObsoleteEx(
            ReplacementTypeOrMember = "RabbitMQTransport.ClientCertificate",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "8",
            RemoveInVersion = "9")]
        public RabbitMqTransportLegacySettings SetClientCertificate(string path, string password)
        {
            var cert = new X509Certificate2(path, password);
            Transport.ClientCertificate = cert;
            return this;
        }

        /// <summary>
        /// Sets the interval for heartbeats between the endpoint and the broker.
        /// </summary>
        /// <param name="heartbeatInterval">The time interval to use.</param>
        [ObsoleteEx(
            ReplacementTypeOrMember = "RabbitMQTransport.HeartbeatInterval",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "8",
            RemoveInVersion = "9")]
        public RabbitMqTransportLegacySettings SetHeartbeatInterval(TimeSpan heartbeatInterval)
        {
            Transport.HeartbeatInterval = heartbeatInterval;
            return this;
        }

        /// <summary>
        /// Sets the time to wait between attempts to reconnect to the broker if the connection is lost.
        /// </summary>
        /// <param name="networkRecoveryInterval">The time interval to use.</param>
        [ObsoleteEx(
            ReplacementTypeOrMember = "RabbitMQTransport.NetworkRecoveryInterval",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "8",
            RemoveInVersion = "9")]
        public RabbitMqTransportLegacySettings SetNetworkRecoveryInterval(TimeSpan networkRecoveryInterval)
        {
            Transport.NetworkRecoveryInterval = networkRecoveryInterval;
            return this;
        }

        /// <summary>
        /// Overrides the default time to wait before triggering a circuit breaker that initiates the endpoint shutdown procedure when the message pump's connection to the broker is lost and cannot be recovered.
        /// </summary>
        /// <param name="waitTime">The time to wait before triggering the circuit breaker.</param>
        [ObsoleteEx(
            ReplacementTypeOrMember = "RabbitMQTransport.TimeToWaitBeforeTriggeringCircuitBreaker",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "8",
            RemoveInVersion = "9")]
        public RabbitMqTransportLegacySettings TimeToWaitBeforeTriggeringCircuitBreaker(TimeSpan waitTime)
        {
            Transport.TimeToWaitBeforeTriggeringCircuitBreaker = waitTime;
            return this;
        }

        /// <summary>
        /// Uses the conventional routing topology.
        /// </summary>
        [ObsoleteEx(
            ReplacementTypeOrMember = "RabbitMQTransport.RoutingTopology",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "8",
            RemoveInVersion = "9")]
        public RabbitMqTransportLegacySettings UseConventionalRoutingTopology()
        {
            Transport.RoutingTopology = new ConventionalRoutingTopology(true);
            return this;
        }

        /// <summary>
        /// Uses the direct routing topology with the specified conventions.
        /// </summary>
        /// <param name="routingKeyConvention">The routing key convention.</param>
        /// <param name="exchangeNameConvention">The exchange name convention.</param>
        [ObsoleteEx(
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "8",
            RemoveInVersion = "9")]
        public RabbitMqTransportLegacySettings UseDirectRoutingTopology(
            Func<Type, string> routingKeyConvention = null, Func<string> exchangeNameConvention = null)
        {
            Transport.RoutingTopology = new DirectRoutingTopology(true, exchangeNameConvention, routingKeyConvention);
            return this;
        }

        /// <summary>
        /// Specifies that an external authentication mechanism should be used for client authentication.
        /// </summary>
        /// <returns></returns>
        [ObsoleteEx(
            ReplacementTypeOrMember = "RabbitMQTransport.UseExternalAuthMechanism",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "8",
            RemoveInVersion = "9")]
        public RabbitMqTransportLegacySettings UseExternalAuthMechanism()
        {
            Transport.UseExternalAuthMechanism = true;
            return this;
        }
    }
}
#pragma warning restore 618
#pragma warning restore 1591
