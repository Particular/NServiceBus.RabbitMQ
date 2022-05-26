namespace NServiceBus
{
    using System;
    using System.Security.Cryptography.X509Certificates;
    using NServiceBus.Transport.RabbitMQ;

    /// <summary>
    /// Adds access to the RabbitMQ transport config to the global Transports object.
    /// </summary>
    public static partial class RabbitMQTransportSettingsExtensions
    {
        /// <summary>
        /// Configures NServiceBus to use the given transport.
        /// </summary>
        [PreObsolete(
            RemoveInVersion = "10",
            TreatAsErrorFromVersion = "9",
            ReplacementTypeOrMember = "EndpointConfiguration.UseTransport(TransportDefinition)")]
        public static TransportExtensions<RabbitMQTransport> UseTransport<T>(this EndpointConfiguration config) where T : RabbitMQTransport
        {
            var transport = new RabbitMQTransport();

            var routing = config.UseTransport(transport);

            var settings = new TransportExtensions<RabbitMQTransport>(transport, routing);

            return settings;
        }

        /// <summary>
        /// The connection string to use when connecting to the broker.
        /// </summary>
        [PreObsolete(
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "9",
            RemoveInVersion = "10")]
        public static TransportExtensions<RabbitMQTransport> ConnectionString(this TransportExtensions<RabbitMQTransport> transport, string connectionString)
        {
            transport.Transport.LegacyApiConnectionString = connectionString;
            return transport;
        }

        /// <summary>
        /// The connection string to use when connecting to the broker.
        /// </summary>
        [PreObsolete(
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "9",
            RemoveInVersion = "10")]
        public static TransportExtensions<RabbitMQTransport> ConnectionString(this TransportExtensions<RabbitMQTransport> transport, Func<string> getConnectionString)
        {
            transport.Transport.LegacyApiConnectionString = getConnectionString();
            return transport;
        }

        /// <summary>
        /// Allows the user to control how the message ID is determined. Mostly useful when doing native integration with non-NSB endpoints.
        /// </summary>
        /// <param name="transport">The transport settings.</param>
        /// <param name="customIdStrategy">The user-defined strategy for giving the message a unique ID.</param>
        [PreObsolete(
            ReplacementTypeOrMember = "RabbitMQTransport.MessageIdStrategy",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "9",
            RemoveInVersion = "10")]
        public static TransportExtensions<RabbitMQTransport> CustomMessageIdStrategy(this TransportExtensions<RabbitMQTransport> transport, Func<RabbitMQ.Client.Events.BasicDeliverEventArgs, string> customIdStrategy)
        {
            transport.Transport.MessageIdStrategy = customIdStrategy;
            return transport;
        }

        /// <summary>
        /// Specifies that exchanges and queues should be declared as non-durable.
        /// </summary>
        /// <param name="transport"></param>
        [PreObsolete(
           Message = "This is now part of routing topology configuration, which has been moved to the constructor of the RabbitMQTransport class.",
           TreatAsErrorFromVersion = "9",
           RemoveInVersion = "10")]
        public static TransportExtensions<RabbitMQTransport> DisableDurableExchangesAndQueues(this TransportExtensions<RabbitMQTransport> transport)
        {
            transport.Transport.UseDurableExchangesAndQueues = false;
            return transport;
        }

        /// <summary>
        /// Disables all remote certificate validation when connecting to the broker via TLS.
        /// </summary>
        [PreObsolete(
            ReplacementTypeOrMember = "RabbitMQTransport.ValidateRemoteCertificate",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "9",
            RemoveInVersion = "10")]
        public static TransportExtensions<RabbitMQTransport> DisableRemoteCertificateValidation(this TransportExtensions<RabbitMQTransport> transport)
        {
            transport.Transport.ValidateRemoteCertificate = false;
            return transport;
        }

        /// <summary>
        /// Overrides the default prefetch count calculation with the specified value.
        /// </summary>
        /// <param name="transport">The transport settings.</param>
        /// <param name="prefetchCount">The prefetch count to use.</param>
        [PreObsolete(
            ReplacementTypeOrMember = "RabbitMQTransport.PrefetchCountCalculation",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "9",
            RemoveInVersion = "10")]
        public static TransportExtensions<RabbitMQTransport> PrefetchCount(this TransportExtensions<RabbitMQTransport> transport, ushort prefetchCount)
        {
            transport.Transport.PrefetchCountCalculation = _ => prefetchCount;
            return transport;
        }

        /// <summary>
        /// Specifies the multiplier to apply to the maximum concurrency value to calculate the prefetch count.
        /// </summary>
        /// <param name="transport">The transport settings.</param>
        /// <param name="prefetchMultiplier">The multiplier value to use in the prefetch calculation.</param>
        [PreObsolete(
            ReplacementTypeOrMember = "RabbitMQTransport.PrefetchCountCalculation",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "9",
            RemoveInVersion = "10")]
        public static TransportExtensions<RabbitMQTransport> PrefetchMultiplier(this TransportExtensions<RabbitMQTransport> transport, int prefetchMultiplier)
        {
            transport.Transport.PrefetchCountCalculation = concurrency => prefetchMultiplier * concurrency;
            return transport;
        }

        /// <summary>
        /// Specifies the certificate to use for client authentication when connecting to the broker via TLS.
        /// </summary>
        /// <param name="transport">The transport settings.</param>
        /// <param name="clientCertificate">The certificate to use for client authentication.</param>
        [PreObsolete(
            ReplacementTypeOrMember = "RabbitMQTransport.ClientCertificate",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "9",
            RemoveInVersion = "10")]
        public static TransportExtensions<RabbitMQTransport> SetClientCertificate(this TransportExtensions<RabbitMQTransport> transport, X509Certificate2 clientCertificate)
        {
            transport.Transport.ClientCertificate = clientCertificate;
            return transport;
        }

        /// <summary>
        /// Specifies the certificate to use for client authentication when connecting to the broker via TLS.
        /// </summary>
        /// <param name="transport">The transport settings.</param>
        /// <param name="path">The path to the certificate file.</param>
        /// <param name="password">The password for the certificate specified in <paramref name="path"/>.</param>
        [PreObsolete(
            ReplacementTypeOrMember = "RabbitMQTransport.ClientCertificate",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "9",
            RemoveInVersion = "10")]
        public static TransportExtensions<RabbitMQTransport> SetClientCertificate(this TransportExtensions<RabbitMQTransport> transport, string path, string password)
        {
            var cert = new X509Certificate2(path, password);
            transport.Transport.ClientCertificate = cert;
            return transport;
        }

        /// <summary>
        /// Sets the interval for heartbeats between the endpoint and the broker.
        /// </summary>
        /// <param name="transport">The transport settings.</param>
        /// <param name="heartbeatInterval">The time interval to use.</param>
        [PreObsolete(
            ReplacementTypeOrMember = "RabbitMQTransport.HeartbeatInterval",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "9",
            RemoveInVersion = "10")]
        public static TransportExtensions<RabbitMQTransport> SetHeartbeatInterval(this TransportExtensions<RabbitMQTransport> transport, TimeSpan heartbeatInterval)
        {
            transport.Transport.HeartbeatInterval = heartbeatInterval;
            return transport;
        }

        /// <summary>
        /// Sets the time to wait between attempts to reconnect to the broker if the connection is lost.
        /// </summary>
        /// <param name="transport">The transport settings.</param>
        /// <param name="networkRecoveryInterval">The time interval to use.</param>
        [PreObsolete(
            ReplacementTypeOrMember = "RabbitMQTransport.NetworkRecoveryInterval",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "9",
            RemoveInVersion = "10")]
        public static TransportExtensions<RabbitMQTransport> SetNetworkRecoveryInterval(this TransportExtensions<RabbitMQTransport> transport, TimeSpan networkRecoveryInterval)
        {
            transport.Transport.NetworkRecoveryInterval = networkRecoveryInterval;
            return transport;
        }

        /// <summary>
        /// Overrides the default time to wait before triggering a circuit breaker that initiates the endpoint shutdown procedure when the message pump's connection to the broker is lost and cannot be recovered.
        /// </summary>
        /// <param name="transport">The transport settings.</param>
        /// <param name="waitTime">The time to wait before triggering the circuit breaker.</param>
        [PreObsolete(
            ReplacementTypeOrMember = "RabbitMQTransport.TimeToWaitBeforeTriggeringCircuitBreaker",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "9",
            RemoveInVersion = "10")]
        public static TransportExtensions<RabbitMQTransport> TimeToWaitBeforeTriggeringCircuitBreaker(this TransportExtensions<RabbitMQTransport> transport, TimeSpan waitTime)
        {
            transport.Transport.TimeToWaitBeforeTriggeringCircuitBreaker = waitTime;
            return transport;
        }

        /// <summary>
        /// Uses the conventional routing topology.
        /// </summary>
        /// <param name="transport">The transport settings.</param>
        /// <param name="queueType">The type of queue that the endpoint should use.</param>
        [PreObsolete(
            Message = "Routing topology configuration has been moved to the constructor of the RabbitMQTransport class.",
            TreatAsErrorFromVersion = "9",
            RemoveInVersion = "10")]
        public static TransportExtensions<RabbitMQTransport> UseConventionalRoutingTopology(this TransportExtensions<RabbitMQTransport> transport, QueueType queueType)
        {
            transport.Transport.TopologyFactory = durable => new ConventionalRoutingTopology(durable, queueType);
            return transport;
        }

        /// <summary>
        /// Registers a custom routing topology.
        /// </summary>
        /// <param name="transport"></param>
        /// <param name="topologyFactory">The function used to create the routing topology instance. The parameter of the function indicates whether exchanges and queues declared by the routing topology should be durable.</param>
        /// <returns></returns>
        [PreObsolete(
            Message = "Routing topology configuration has been moved to the constructor of the RabbitMQTransport class.",
            TreatAsErrorFromVersion = "9",
            RemoveInVersion = "10")]
        public static TransportExtensions<RabbitMQTransport> UseCustomRoutingTopology(this TransportExtensions<RabbitMQTransport> transport, Func<bool, IRoutingTopology> topologyFactory)
        {
            transport.Transport.TopologyFactory = topologyFactory;
            return transport;
        }

        /// <summary>
        /// Uses the direct routing topology with the specified conventions.
        /// </summary>
        /// <param name="transport">The transport settings.</param>
        /// <param name="queueType">The type of queue that the endpoint should use.</param>
        /// <param name="routingKeyConvention">The routing key convention.</param>
        /// <param name="exchangeNameConvention">The exchange name convention.</param>
        [PreObsolete(
            Message = "Routing topology configuration has been moved to the constructor of the RabbitMQTransport class.",
            TreatAsErrorFromVersion = "9",
            RemoveInVersion = "10")]
        public static TransportExtensions<RabbitMQTransport> UseDirectRoutingTopology(this TransportExtensions<RabbitMQTransport> transport, QueueType queueType, Func<Type, string> routingKeyConvention = null, Func<string> exchangeNameConvention = null)
        {
            transport.Transport.TopologyFactory = durable => new DirectRoutingTopology(durable, queueType, exchangeNameConvention, routingKeyConvention);
            return transport;
        }

        /// <summary>
        /// Specifies that an external authentication mechanism should be used for client authentication.
        /// </summary>
        /// <returns></returns>
        [PreObsolete(
            ReplacementTypeOrMember = "RabbitMQTransport.UseExternalAuthMechanism",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "9",
            RemoveInVersion = "10")]
        public static TransportExtensions<RabbitMQTransport> UseExternalAuthMechanism(this TransportExtensions<RabbitMQTransport> transport)
        {
            transport.Transport.UseExternalAuthMechanism = true;
            return transport;
        }
    }
}
