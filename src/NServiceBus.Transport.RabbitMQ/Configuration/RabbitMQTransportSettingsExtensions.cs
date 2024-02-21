namespace NServiceBus
{
    using System;
    using System.Security.Cryptography.X509Certificates;
    using NServiceBus.Transport.RabbitMQ;

    /// <summary>
    /// Adds access to the RabbitMQ transport config to the global Transports object.
    /// </summary>
    [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
        Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
    public static class RabbitMQTransportSettingsExtensions
    {
        /// <summary>
        /// Configures NServiceBus to use the given transport.
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "EndpointConfiguration.UseTransport(TransportDefinition)",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<RabbitMQTransport> UseTransport<T>(this EndpointConfiguration config) where T : RabbitMQTransport
        {
            ArgumentNullException.ThrowIfNull(config);

            var transport = new RabbitMQTransport();

            var routing = config.UseTransport(transport);

            var settings = new TransportExtensions<RabbitMQTransport>(transport, routing);

            return settings;
        }

        /// <summary>
        ///  Adds an additional cluster node that the endpoint can use to connect to the broker.
        /// </summary>
        /// <param name="transportExtensions">The transport settings.</param>
        /// <param name="hostName">The hostname of the node.</param>
        /// <param name="useTls">Indicates if the connection to the node should be secured with TLS.</param>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<RabbitMQTransport> AddClusterNode(this TransportExtensions<RabbitMQTransport> transportExtensions, string hostName, bool useTls)
        {
            ArgumentNullException.ThrowIfNull(transportExtensions);

            transportExtensions.Transport.AddClusterNode(hostName, useTls);
            return transportExtensions;
        }

        /// <summary>
        /// Adds an additional cluster node that the endpoint can use to connect to the broker.
        /// </summary>
        /// <param name="transportExtensions">The transport settings.</param>
        /// <param name="hostName">The hostname of the node.</param>
        /// <param name="port">The port of the node.</param>
        /// <param name="useTls">Indicates if the connection to the node should be secured with TLS.</param>
        /// <returns></returns>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<RabbitMQTransport> AddClusterNode(this TransportExtensions<RabbitMQTransport> transportExtensions, string hostName, int port, bool useTls)
        {
            ArgumentNullException.ThrowIfNull(transportExtensions);

            transportExtensions.Transport.AddClusterNode(hostName, port, useTls);
            return transportExtensions;
        }

        /// <summary>
        /// The connection string to use when connecting to the broker.
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<RabbitMQTransport> ConnectionString(this TransportExtensions<RabbitMQTransport> transportExtensions, string connectionString)
        {
            ArgumentNullException.ThrowIfNull(transportExtensions);
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

            transportExtensions.Transport.LegacyApiConnectionString = connectionString;
            return transportExtensions;
        }

        /// <summary>
        /// The connection string to use when connecting to the broker.
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<RabbitMQTransport> ConnectionString(this TransportExtensions<RabbitMQTransport> transportExtensions, Func<string> getConnectionString)
        {
            ArgumentNullException.ThrowIfNull(transportExtensions);
            ArgumentNullException.ThrowIfNull(getConnectionString);

            transportExtensions.Transport.LegacyApiConnectionString = getConnectionString();
            return transportExtensions;
        }

        /// <summary>
        /// Allows the user to control how the message ID is determined. Mostly useful when consuming native messages from non-NServiceBus endpoints.
        /// </summary>
        /// <param name="transportExtensions">The transport settings.</param>
        /// <param name="customIdStrategy">The user-defined strategy for giving the message a unique ID.</param>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "RabbitMQTransport.MessageIdStrategy",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<RabbitMQTransport> CustomMessageIdStrategy(this TransportExtensions<RabbitMQTransport> transportExtensions, Func<RabbitMQ.Client.Events.BasicDeliverEventArgs, string> customIdStrategy)
        {
            ArgumentNullException.ThrowIfNull(transportExtensions);
            ArgumentNullException.ThrowIfNull(customIdStrategy);

            transportExtensions.Transport.MessageIdStrategy = customIdStrategy;
            return transportExtensions;
        }

        /// <summary>
        /// Specifies that exchanges and queues should be declared as non-durable.
        /// </summary>
        /// <param name="transportExtensions"></param>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            Message = "This is now part of routing topology configuration, which has been moved to the constructor of the RabbitMQTransport class.",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<RabbitMQTransport> DisableDurableExchangesAndQueues(this TransportExtensions<RabbitMQTransport> transportExtensions)
        {
            ArgumentNullException.ThrowIfNull(transportExtensions);

            transportExtensions.Transport.UseDurableExchangesAndQueues = false;
            return transportExtensions;
        }

        /// <summary>
        /// Disables all remote certificate validation when connecting to the broker via TLS.
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "RabbitMQTransport.ValidateRemoteCertificate",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<RabbitMQTransport> DisableRemoteCertificateValidation(this TransportExtensions<RabbitMQTransport> transportExtensions)
        {
            ArgumentNullException.ThrowIfNull(transportExtensions);

            transportExtensions.Transport.ValidateRemoteCertificate = false;
            return transportExtensions;
        }

        /// <summary>
        /// Overrides the default prefetch count calculation with the specified value.
        /// </summary>
        /// <param name="transportExtensions">The transport settings.</param>
        /// <param name="prefetchCount">The prefetch count to use.</param>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "RabbitMQTransport.PrefetchCountCalculation",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<RabbitMQTransport> PrefetchCount(this TransportExtensions<RabbitMQTransport> transportExtensions, ushort prefetchCount)
        {
            ArgumentNullException.ThrowIfNull(transportExtensions);

            transportExtensions.Transport.PrefetchCountCalculation = _ => prefetchCount;
            return transportExtensions;
        }

        /// <summary>
        /// Specifies the multiplier to apply to the maximum concurrency value to calculate the prefetch count.
        /// </summary>
        /// <param name="transportExtensions">The transport settings.</param>
        /// <param name="prefetchMultiplier">The multiplier value to use in the prefetch calculation.</param>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "RabbitMQTransport.PrefetchCountCalculation",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<RabbitMQTransport> PrefetchMultiplier(this TransportExtensions<RabbitMQTransport> transportExtensions, int prefetchMultiplier)
        {
            ArgumentNullException.ThrowIfNull(transportExtensions);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(prefetchMultiplier);

            transportExtensions.Transport.PrefetchCountCalculation = concurrency => prefetchMultiplier * concurrency;
            return transportExtensions;
        }

        /// <summary>
        /// Specifies the certificate to use for client authentication when connecting to the broker via TLS.
        /// </summary>
        /// <param name="transportExtensions">The transport settings.</param>
        /// <param name="clientCertificate">The certificate to use for client authentication.</param>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "RabbitMQTransport.ClientCertificate",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<RabbitMQTransport> SetClientCertificate(this TransportExtensions<RabbitMQTransport> transportExtensions, X509Certificate2 clientCertificate)
        {
            ArgumentNullException.ThrowIfNull(transportExtensions);
            ArgumentNullException.ThrowIfNull(clientCertificate);

            transportExtensions.Transport.ClientCertificate = clientCertificate;
            return transportExtensions;
        }

        /// <summary>
        /// Specifies the certificate to use for client authentication when connecting to the broker via TLS.
        /// </summary>
        /// <param name="transportExtensions">The transport settings.</param>
        /// <param name="path">The path to the certificate file.</param>
        /// <param name="password">The password for the certificate specified in <paramref name="path"/>.</param>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "RabbitMQTransport.ClientCertificate",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<RabbitMQTransport> SetClientCertificate(this TransportExtensions<RabbitMQTransport> transportExtensions, string path, string password)
        {
            ArgumentNullException.ThrowIfNull(transportExtensions);
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            ArgumentException.ThrowIfNullOrWhiteSpace(password);

            transportExtensions.Transport.ClientCertificate = new X509Certificate2(path, password);
            return transportExtensions;
        }

        /// <summary>
        /// Sets the interval for heartbeats between the endpoint and the broker.
        /// </summary>
        /// <param name="transportExtensions">The transport settings.</param>
        /// <param name="heartbeatInterval">The time interval to use.</param>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "RabbitMQTransport.HeartbeatInterval",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<RabbitMQTransport> SetHeartbeatInterval(this TransportExtensions<RabbitMQTransport> transportExtensions, TimeSpan heartbeatInterval)
        {
            ArgumentNullException.ThrowIfNull(transportExtensions);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(heartbeatInterval, TimeSpan.Zero);

            transportExtensions.Transport.HeartbeatInterval = heartbeatInterval;
            return transportExtensions;
        }

        /// <summary>
        /// Sets the time to wait between attempts to reconnect to the broker if the connection is lost.
        /// </summary>
        /// <param name="transportExtensions">The transport settings.</param>
        /// <param name="networkRecoveryInterval">The time interval to use.</param>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "RabbitMQTransport.NetworkRecoveryInterval",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<RabbitMQTransport> SetNetworkRecoveryInterval(this TransportExtensions<RabbitMQTransport> transportExtensions, TimeSpan networkRecoveryInterval)
        {
            ArgumentNullException.ThrowIfNull(transportExtensions);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(networkRecoveryInterval, TimeSpan.Zero);

            transportExtensions.Transport.NetworkRecoveryInterval = networkRecoveryInterval;
            return transportExtensions;
        }

        /// <summary>
        /// Sets how long to wait before executing the critical error action when the endpoint cannot communicate with the broker.
        /// </summary>
        /// <param name="transportExtensions">The transport settings.</param>
        /// <param name="waitTime">The time to wait before triggering the circuit breaker.</param>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "RabbitMQTransport.TimeToWaitBeforeTriggeringCircuitBreaker",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<RabbitMQTransport> TimeToWaitBeforeTriggeringCircuitBreaker(this TransportExtensions<RabbitMQTransport> transportExtensions, TimeSpan waitTime)
        {
            ArgumentNullException.ThrowIfNull(transportExtensions);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(waitTime, TimeSpan.Zero);

            transportExtensions.Transport.TimeToWaitBeforeTriggeringCircuitBreaker = waitTime;
            return transportExtensions;
        }

        /// <summary>
        /// Uses the conventional routing topology. This is the preferred setting for new projects.
        /// </summary>
        /// <param name="transportExtensions">The transport settings.</param>
        /// <param name="queueType">The type of queue that the endpoint should use.</param>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            Message = "Routing topology configuration has been moved to the constructor of the RabbitMQTransport class.",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<RabbitMQTransport> UseConventionalRoutingTopology(this TransportExtensions<RabbitMQTransport> transportExtensions, QueueType queueType)
        {
            ArgumentNullException.ThrowIfNull(transportExtensions);

            transportExtensions.Transport.TopologyFactory = durable => new ConventionalRoutingTopology(durable, queueType);
            return transportExtensions;
        }

        /// <summary>
        /// Registers a custom routing topology.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="topologyFactory">The function used to create the routing topology instance. The parameter of the function indicates whether exchanges and queues declared by the routing topology should be durable.</param>
        /// <returns></returns>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            Message = "Routing topology configuration has been moved to the constructor of the RabbitMQTransport class.",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<RabbitMQTransport> UseCustomRoutingTopology(this TransportExtensions<RabbitMQTransport> transportExtensions, Func<bool, IRoutingTopology> topologyFactory)
        {
            ArgumentNullException.ThrowIfNull(transportExtensions);
            ArgumentNullException.ThrowIfNull(topologyFactory);

            transportExtensions.Transport.TopologyFactory = topologyFactory;
            return transportExtensions;
        }

        /// <summary>
        /// Uses the direct routing topology with the specified conventions.
        /// </summary>
        /// <param name="transportExtensions">The transport settings.</param>
        /// <param name="queueType">The type of queue that the endpoint should use.</param>
        /// <param name="routingKeyConvention">The routing key convention.</param>
        /// <param name="exchangeNameConvention">The exchange name convention.</param>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            Message = "Routing topology configuration has been moved to the constructor of the RabbitMQTransport class.",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<RabbitMQTransport> UseDirectRoutingTopology(this TransportExtensions<RabbitMQTransport> transportExtensions, QueueType queueType, Func<Type, string> routingKeyConvention = null, Func<string> exchangeNameConvention = null)
        {
            ArgumentNullException.ThrowIfNull(transportExtensions);

            transportExtensions.Transport.TopologyFactory = durable => new DirectRoutingTopology(durable, queueType, routingKeyConvention, exchangeNameConvention);
            return transportExtensions;
        }

        /// <summary>
        /// Specifies that an external authentication mechanism should be used for client authentication.
        /// </summary>
        /// <returns></returns>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "RabbitMQTransport.UseExternalAuthMechanism",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<RabbitMQTransport> UseExternalAuthMechanism(this TransportExtensions<RabbitMQTransport> transportExtensions)
        {
            ArgumentNullException.ThrowIfNull(transportExtensions);

            transportExtensions.Transport.UseExternalAuthMechanism = true;
            return transportExtensions;
        }
    }
}
