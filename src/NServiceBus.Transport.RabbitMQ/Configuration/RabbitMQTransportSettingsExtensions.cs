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
        ///  Adds an additional cluster node that the endpoint can use to connect to the broker.
        /// </summary>
        /// <param name="transportExtensions">The transport settings.</param>
        /// <param name="hostName">The hostname of the node.</param>
        /// <param name="useTls">Indicates if the connection to the node should be secured with TLS.</param>
        [PreObsolete(
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "9",
            RemoveInVersion = "10")]
        public static TransportExtensions<RabbitMQTransport> AddClusterNode(this TransportExtensions<RabbitMQTransport> transportExtensions, string hostName, bool useTls)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);

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
        [PreObsolete(
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "9",
            RemoveInVersion = "10")]
        public static TransportExtensions<RabbitMQTransport> AddClusterNode(this TransportExtensions<RabbitMQTransport> transportExtensions, string hostName, int port, bool useTls)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);

            transportExtensions.Transport.AddClusterNode(hostName, port, useTls);
            return transportExtensions;
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
        /// Allows the user to control how the message ID is determined. Mostly useful when consuming native messages from non-NServiceBus endpoints.
        /// </summary>
        /// <param name="transportExtensions">The transport settings.</param>
        /// <param name="customIdStrategy">The user-defined strategy for giving the message a unique ID.</param>
        [PreObsolete(
            ReplacementTypeOrMember = "RabbitMQTransport.MessageIdStrategy",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "9",
            RemoveInVersion = "10")]
        public static TransportExtensions<RabbitMQTransport> CustomMessageIdStrategy(this TransportExtensions<RabbitMQTransport> transportExtensions, Func<RabbitMQ.Client.Events.BasicDeliverEventArgs, string> customIdStrategy)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            Guard.AgainstNull(nameof(customIdStrategy), customIdStrategy);

            transportExtensions.Transport.MessageIdStrategy = customIdStrategy;
            return transportExtensions;
        }

        /// <summary>
        /// Specifies that exchanges and queues should be declared as non-durable.
        /// </summary>
        /// <param name="transportExtensions"></param>
        [PreObsolete(
           Message = "This is now part of routing topology configuration, which has been moved to the constructor of the RabbitMQTransport class.",
           TreatAsErrorFromVersion = "9",
           RemoveInVersion = "10")]
        public static TransportExtensions<RabbitMQTransport> DisableDurableExchangesAndQueues(this TransportExtensions<RabbitMQTransport> transportExtensions)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);

            transportExtensions.Transport.UseDurableExchangesAndQueues = false;
            return transportExtensions;
        }

        /// <summary>
        /// Disables all remote certificate validation when connecting to the broker via TLS.
        /// </summary>
        [PreObsolete(
            ReplacementTypeOrMember = "RabbitMQTransport.ValidateRemoteCertificate",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "9",
            RemoveInVersion = "10")]
        public static TransportExtensions<RabbitMQTransport> DisableRemoteCertificateValidation(this TransportExtensions<RabbitMQTransport> transportExtensions)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);

            transportExtensions.Transport.ValidateRemoteCertificate = false;
            return transportExtensions;
        }

        /// <summary>
        /// Overrides the default prefetch count calculation with the specified value.
        /// </summary>
        /// <param name="transportExtensions">The transport settings.</param>
        /// <param name="prefetchCount">The prefetch count to use.</param>
        [PreObsolete(
            ReplacementTypeOrMember = "RabbitMQTransport.PrefetchCountCalculation",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "9",
            RemoveInVersion = "10")]
        public static TransportExtensions<RabbitMQTransport> PrefetchCount(this TransportExtensions<RabbitMQTransport> transportExtensions, ushort prefetchCount)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);

            transportExtensions.Transport.PrefetchCountCalculation = _ => prefetchCount;
            return transportExtensions;
        }

        /// <summary>
        /// Specifies the multiplier to apply to the maximum concurrency value to calculate the prefetch count.
        /// </summary>
        /// <param name="transportExtensions">The transport settings.</param>
        /// <param name="prefetchMultiplier">The multiplier value to use in the prefetch calculation.</param>
        [PreObsolete(
            ReplacementTypeOrMember = "RabbitMQTransport.PrefetchCountCalculation",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "9",
            RemoveInVersion = "10")]
        public static TransportExtensions<RabbitMQTransport> PrefetchMultiplier(this TransportExtensions<RabbitMQTransport> transportExtensions, int prefetchMultiplier)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            Guard.AgainstNegativeAndZero(nameof(prefetchMultiplier), prefetchMultiplier);

            transportExtensions.Transport.PrefetchCountCalculation = concurrency => prefetchMultiplier * concurrency;
            return transportExtensions;
        }

        /// <summary>
        /// Specifies the certificate to use for client authentication when connecting to the broker via TLS.
        /// </summary>
        /// <param name="transportExtensions">The transport settings.</param>
        /// <param name="clientCertificate">The certificate to use for client authentication.</param>
        [PreObsolete(
            ReplacementTypeOrMember = "RabbitMQTransport.ClientCertificate",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "9",
            RemoveInVersion = "10")]
        public static TransportExtensions<RabbitMQTransport> SetClientCertificate(this TransportExtensions<RabbitMQTransport> transportExtensions, X509Certificate2 clientCertificate)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            Guard.AgainstNull(nameof(clientCertificate), clientCertificate);

            transportExtensions.Transport.ClientCertificate = clientCertificate;
            return transportExtensions;
        }

        /// <summary>
        /// Specifies the certificate to use for client authentication when connecting to the broker via TLS.
        /// </summary>
        /// <param name="transportExtensions">The transport settings.</param>
        /// <param name="path">The path to the certificate file.</param>
        /// <param name="password">The password for the certificate specified in <paramref name="path"/>.</param>
        [PreObsolete(
            ReplacementTypeOrMember = "RabbitMQTransport.ClientCertificate",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "9",
            RemoveInVersion = "10")]
        public static TransportExtensions<RabbitMQTransport> SetClientCertificate(this TransportExtensions<RabbitMQTransport> transportExtensions, string path, string password)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            Guard.AgainstNullAndEmpty(nameof(path), path);
            Guard.AgainstNullAndEmpty(nameof(password), password);

            transportExtensions.Transport.ClientCertificate = new X509Certificate2(path, password);
            return transportExtensions;
        }

        /// <summary>
        /// Sets the interval for heartbeats between the endpoint and the broker.
        /// </summary>
        /// <param name="transportExtensions">The transport settings.</param>
        /// <param name="heartbeatInterval">The time interval to use.</param>
        [PreObsolete(
            ReplacementTypeOrMember = "RabbitMQTransport.HeartbeatInterval",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "9",
            RemoveInVersion = "10")]
        public static TransportExtensions<RabbitMQTransport> SetHeartbeatInterval(this TransportExtensions<RabbitMQTransport> transportExtensions, TimeSpan heartbeatInterval)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            Guard.AgainstNegativeAndZero(nameof(heartbeatInterval), heartbeatInterval);

            transportExtensions.Transport.HeartbeatInterval = heartbeatInterval;
            return transportExtensions;
        }

        /// <summary>
        /// Sets the time to wait between attempts to reconnect to the broker if the connection is lost.
        /// </summary>
        /// <param name="transportExtensions">The transport settings.</param>
        /// <param name="networkRecoveryInterval">The time interval to use.</param>
        [PreObsolete(
            ReplacementTypeOrMember = "RabbitMQTransport.NetworkRecoveryInterval",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "9",
            RemoveInVersion = "10")]
        public static TransportExtensions<RabbitMQTransport> SetNetworkRecoveryInterval(this TransportExtensions<RabbitMQTransport> transportExtensions, TimeSpan networkRecoveryInterval)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            Guard.AgainstNegativeAndZero(nameof(networkRecoveryInterval), networkRecoveryInterval);

            transportExtensions.Transport.NetworkRecoveryInterval = networkRecoveryInterval;
            return transportExtensions;
        }

        /// <summary>
        /// Sets how long to wait before executing the critical error action when the endpoint cannot communicate with the broker.
        /// </summary>
        /// <param name="transportExtensions">The transport settings.</param>
        /// <param name="waitTime">The time to wait before triggering the circuit breaker.</param>
        [PreObsolete(
            ReplacementTypeOrMember = "RabbitMQTransport.TimeToWaitBeforeTriggeringCircuitBreaker",
            Message = "The configuration has been moved to RabbitMQTransport class.",
            TreatAsErrorFromVersion = "9",
            RemoveInVersion = "10")]
        public static TransportExtensions<RabbitMQTransport> TimeToWaitBeforeTriggeringCircuitBreaker(this TransportExtensions<RabbitMQTransport> transportExtensions, TimeSpan waitTime)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            Guard.AgainstNegativeAndZero(nameof(waitTime), waitTime);

            transportExtensions.Transport.TimeToWaitBeforeTriggeringCircuitBreaker = waitTime;
            return transportExtensions;
        }

        /// <summary>
        /// Uses the conventional routing topology. This is the preferred setting for new projects.
        /// </summary>
        /// <param name="transportExtensions">The transport settings.</param>
        /// <param name="queueType">The type of queue that the endpoint should use.</param>
        [PreObsolete(
            Message = "Routing topology configuration has been moved to the constructor of the RabbitMQTransport class.",
            TreatAsErrorFromVersion = "9",
            RemoveInVersion = "10")]
        public static TransportExtensions<RabbitMQTransport> UseConventionalRoutingTopology(this TransportExtensions<RabbitMQTransport> transportExtensions, QueueType queueType)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);

            transportExtensions.Transport.TopologyFactory = durable => new ConventionalRoutingTopology(durable, queueType);
            return transportExtensions;
        }

        /// <summary>
        /// Registers a custom routing topology.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="topologyFactory">The function used to create the routing topology instance. The parameter of the function indicates whether exchanges and queues declared by the routing topology should be durable.</param>
        /// <returns></returns>
        [PreObsolete(
            Message = "Routing topology configuration has been moved to the constructor of the RabbitMQTransport class.",
            TreatAsErrorFromVersion = "9",
            RemoveInVersion = "10")]
        public static TransportExtensions<RabbitMQTransport> UseCustomRoutingTopology(this TransportExtensions<RabbitMQTransport> transportExtensions, Func<bool, IRoutingTopology> topologyFactory)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            Guard.AgainstNull(nameof(topologyFactory), topologyFactory);

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
        [PreObsolete(
            Message = "Routing topology configuration has been moved to the constructor of the RabbitMQTransport class.",
            TreatAsErrorFromVersion = "9",
            RemoveInVersion = "10")]
        public static TransportExtensions<RabbitMQTransport> UseDirectRoutingTopology(this TransportExtensions<RabbitMQTransport> transportExtensions, QueueType queueType, Func<Type, string> routingKeyConvention = null, Func<string> exchangeNameConvention = null)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);

            transportExtensions.Transport.TopologyFactory = durable => new DirectRoutingTopology(durable, queueType, routingKeyConvention, exchangeNameConvention);
            return transportExtensions;
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
        public static TransportExtensions<RabbitMQTransport> UseExternalAuthMechanism(this TransportExtensions<RabbitMQTransport> transportExtensions)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);

            transportExtensions.Transport.UseExternalAuthMechanism = true;
            return transportExtensions;
        }
    }
}
