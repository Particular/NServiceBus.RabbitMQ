namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using RabbitMQ.Client;
    using RabbitMQ.Client.Events;
    using Transport;
    using Transport.RabbitMQ;
    using Transport.RabbitMQ.ManagementClient;
    using ConnectionFactory = Transport.RabbitMQ.ConnectionFactory;

    /// <summary>
    /// Transport definition for RabbitMQ.
    /// </summary>
    public class RabbitMQTransport : TransportDefinition
    {
        TimeSpan heartbeatInterval = TimeSpan.FromSeconds(60);
        TimeSpan networkRecoveryInterval = TimeSpan.FromSeconds(10);
        Func<BasicDeliverEventArgs, string> messageIdStrategy = MessageConverter.DefaultMessageIdStrategy;
        PrefetchCountCalculation prefetchCountCalculation = maxConcurrency => 3 * maxConcurrency;
        TimeSpan timeToWaitBeforeTriggeringCircuitBreaker = TimeSpan.FromMinutes(2);

        readonly List<(string hostName, int port, bool useTls)> additionalClusterNodes = [];

        /// <summary>
        /// Creates a new instance of the RabbitMQ transport.
        /// </summary>
        /// <param name="routingTopology">The routing topology to use.</param>
        /// <param name="connectionString">The connection string to use when connecting to the broker.</param>
        public RabbitMQTransport(RoutingTopology routingTopology, string connectionString)
            : base(TransportTransactionMode.ReceiveOnly,
                supportsDelayedDelivery: true,
                supportsPublishSubscribe: true,
                supportsTTBR: true)
        {
            ArgumentNullException.ThrowIfNull(routingTopology);
            ArgumentNullException.ThrowIfNull(connectionString);

            RoutingTopology = routingTopology.Create();
            ConnectionConfiguration = ConnectionConfiguration.Create(connectionString);
        }

        /// <summary>
        /// Creates a new instance of the RabbitMQ transport.
        /// </summary>
        /// <param name="routingTopology">The routing topology to use.</param>
        /// <param name="connectionString">The connection string to use when connecting to the broker.</param>
        /// <param name="enableDelayedDelivery">Should the delayed delivery infrastructure be created by the endpoint</param>
        public RabbitMQTransport(RoutingTopology routingTopology, string connectionString, bool enableDelayedDelivery)
            : base(TransportTransactionMode.ReceiveOnly,
                supportsDelayedDelivery: enableDelayedDelivery,
                supportsPublishSubscribe: true,
                supportsTTBR: true)
        {
            ArgumentNullException.ThrowIfNull(routingTopology);
            ArgumentNullException.ThrowIfNull(connectionString);

            RoutingTopology = routingTopology.Create();
            ConnectionConfiguration = ConnectionConfiguration.Create(connectionString);
        }

        internal ConnectionConfiguration ConnectionConfiguration { get; set; }

        internal IRoutingTopology RoutingTopology { get; set; }

        /// <summary>
        /// The strategy for deriving the message ID from the raw RabbitMQ message. Override in case of native integration when
        /// the sender of the message is not an NServiceBus endpoint.
        /// </summary>
        public Func<BasicDeliverEventArgs, string> MessageIdStrategy
        {
            get => messageIdStrategy;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                messageIdStrategy = value;
            }
        }

        /// <summary>
        /// The time to wait before executing the critical error action when the endpoint cannot communicate with the broker.
        /// </summary>
        public TimeSpan TimeToWaitBeforeTriggeringCircuitBreaker
        {
            get => timeToWaitBeforeTriggeringCircuitBreaker;
            set
            {
                ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
                timeToWaitBeforeTriggeringCircuitBreaker = value;
            }
        }

        /// <summary>
        /// Gets or sets the action that allows customization of the native <see cref="BasicProperties"/>
        /// just before it is dispatched to the rabbitmq client.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When provided, the action is invoked after all other transport customizations have executed.
        /// This means that any changes made by the native customization logic can override or conflict
        /// with previous transport-level adjustments. This extension point should be used with caution,
        /// as modifying a native message at this stage can lead to unintended behavior if the message
        /// content or properties are altered in ways that do not align with expectations.
        /// with expectations elsewhere in the system.
        /// </para>
        /// </remarks>
        public Action<IOutgoingTransportOperation, IBasicProperties> OutgoingNativeMessageCustomization { get; set; }

        /// <summary>
        /// The calculation method for the prefetch count. The default is 3 times the maximum concurrency value.
        /// </summary>
        public PrefetchCountCalculation PrefetchCountCalculation
        {
            get => prefetchCountCalculation;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                prefetchCountCalculation = value;
            }
        }

        /// <summary>
        /// The certificate to use for client authentication when connecting to the broker via TLS.
        /// </summary>
        public X509Certificate2 ClientCertificate { get; set; }

        /// <summary>
        /// Should the client validate the broker certificate when connecting via TLS.
        /// </summary>
        public bool ValidateRemoteCertificate { get; set; } = true;

        /// <summary>
        /// Specifies if an external authentication mechanism should be used for client authentication.
        /// </summary>
        public bool UseExternalAuthMechanism { get; set; } = false;

        /// <summary>
        /// Set this to prevent the transport from using the RabbitMQ Management API.
        /// This is not recommended as it can prevent the transport from setting appropriate delivery limits for retry functionality.
        /// </summary>
        public bool DoNotUseManagementClient { get; set; } = false;

        /// <summary>
        /// Basic authentication HTTP connection string to the RabbitMQ management API.
        /// </summary>
        /// <remarks>
        /// E.g. https://username:password@localhost:15671
        /// </remarks>
        public string ManagementApiUrl { get; set; }

        /// <summary>
        /// The interval for heartbeats between the endpoint and the broker.
        /// </summary>
        public TimeSpan HeartbeatInterval
        {
            get => heartbeatInterval;
            set
            {
                ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
                heartbeatInterval = value;
            }
        }

        /// <summary>
        /// The time to wait between attempts to reconnect to the broker if the connection is lost.
        /// </summary>
        public TimeSpan NetworkRecoveryInterval
        {
            get => networkRecoveryInterval;
            set
            {
                ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
                networkRecoveryInterval = value;
            }
        }

        /// <summary>
        /// Adds an additional cluster node that the endpoint can use to connect to the broker.
        /// </summary>
        /// <param name="hostName">The hostname of the node.</param>
        /// <param name="useTls">Indicates if the connection to the node should be secured with TLS.</param>
        public void AddClusterNode(string hostName, bool useTls)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(hostName);

            additionalClusterNodes.Add((hostName, -1, useTls));
        }

        /// <summary>
        /// Adds an additional cluster node that the endpoint can use to connect to the broker.
        /// </summary>
        /// <param name="hostName">The hostname of the node.</param>
        /// <param name="port">The port of the node.</param>
        /// <param name="useTls">Indicates if the connection to the node should be secured with TLS.</param>
        public void AddClusterNode(string hostName, int port, bool useTls)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(hostName);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);

            additionalClusterNodes.Add((hostName, port, useTls));
        }

        /// <inheritdoc />
        public override async Task<TransportInfrastructure> Initialize(HostSettings hostSettings, ReceiveSettings[] receivers, string[] sendingAddresses, CancellationToken cancellationToken = default)
        {
            ValidateAndApplyLegacyConfiguration();

            X509Certificate2Collection certCollection = null;

            if (ClientCertificate != null)
            {
                certCollection = new X509Certificate2Collection(ClientCertificate);
            }

            var connectionFactory = new ConnectionFactory(
                hostSettings.Name,
                ConnectionConfiguration,
                certCollection,
                !ValidateRemoteCertificate,
                UseExternalAuthMechanism,
                HeartbeatInterval,
                NetworkRecoveryInterval,
                additionalClusterNodes
            );

            var managementClient = !string.IsNullOrEmpty(ManagementApiUrl)
                ? new ManagementClient(ManagementApiUrl, ConnectionConfiguration.VirtualHost)
                : new ManagementClient(ConnectionConfiguration);

            var brokerVerifier = new BrokerVerifier(connectionFactory, !DoNotUseManagementClient, managementClient);
            await brokerVerifier.Initialize(cancellationToken).ConfigureAwait(false);

            var channelProvider = new ChannelProvider(connectionFactory, NetworkRecoveryInterval, RoutingTopology);
            await channelProvider.CreateConnection(cancellationToken).ConfigureAwait(false);

            var converter = new MessageConverter(MessageIdStrategy);

            var infra = new RabbitMQTransportInfrastructure(
                hostSettings,
                receivers,
                connectionFactory,
                RoutingTopology,
                channelProvider,
                converter,
                brokerVerifier,
                OutgoingNativeMessageCustomization,
                TimeToWaitBeforeTriggeringCircuitBreaker,
                PrefetchCountCalculation,
                NetworkRecoveryInterval,
                SupportsDelayedDelivery
            );

            if (hostSettings.SetupInfrastructure)
            {
                await infra.SetupInfrastructure(sendingAddresses, cancellationToken).ConfigureAwait(false);
            }

            return infra;
        }

        /// <inheritdoc />
        public override IReadOnlyCollection<TransportTransactionMode> GetSupportedTransactionModes() => new[] { TransportTransactionMode.ReceiveOnly };

        // Remove all Legacy API stuff below when PreObsoletes are converted

        internal string LegacyApiConnectionString { get; set; }

        internal Func<bool, IRoutingTopology> TopologyFactory { get; set; }

        internal bool UseDurableExchangesAndQueues { get; set; } = true;

        bool legacyMode;

        internal RabbitMQTransport() : base(TransportTransactionMode.ReceiveOnly, true, true, true)
        {
            legacyMode = true;
        }

        void ValidateAndApplyLegacyConfiguration()
        {
            if (!legacyMode)
            {
                return;
            }

            if (TopologyFactory == null)
            {
                throw new Exception("A routing topology must be configured with one of the 'EndpointConfiguration.UseTransport<RabbitMQTransport>().UseXXXXRoutingTopology()` methods. Most new projects should use the Conventional routing topology.");
            }

            RoutingTopology = TopologyFactory(UseDurableExchangesAndQueues);

            if (string.IsNullOrEmpty(LegacyApiConnectionString))
            {
                throw new Exception("A connection string must be configured with 'EndpointConfiguration.UseTransport<RabbitMQTransport>().ConnectionString()` method.");
            }

            ConnectionConfiguration = ConnectionConfiguration.Create(LegacyApiConnectionString);
        }
    }
}