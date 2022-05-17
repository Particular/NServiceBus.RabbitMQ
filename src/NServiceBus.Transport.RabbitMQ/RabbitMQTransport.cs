namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using RabbitMQ.Client.Events;
    using Transport;
    using Transport.RabbitMQ;
    using ConnectionFactory = Transport.RabbitMQ.ConnectionFactory;

    /// <summary>
    /// Transport definition for RabbitMQ.
    /// </summary>
    public partial class RabbitMQTransport : TransportDefinition
    {
        static readonly TransportTransactionMode[] SupportedTransactionModes =
        {
            TransportTransactionMode.None, TransportTransactionMode.ReceiveOnly
        };

        TimeSpan? heartbeatIntervalOverride;
        TimeSpan? networkRecoveryIntervalOverride;
        Func<BasicDeliverEventArgs, string> messageIdStrategy = MessageConverter.DefaultMessageIdStrategy;
        PrefetchCountCalculation prefetchCountCalculation = maxConcurrency => 3 * maxConcurrency;
        TimeSpan timeToWaitBeforeTriggeringCircuitBreaker = TimeSpan.FromMinutes(2);

        internal List<(string, int)> additionalHosts = new List<(string, int)>();

        /// <summary>
        /// Creates new instance of the RabbitMQ transport.
        /// </summary>
        /// <param name="topology">The built-in topology to use.</param>
        /// <param name="connectionString">Connection string.</param>
        /// <param name="queueType">The type of queue to use for receiving queues.</param>
        public RabbitMQTransport(Topology topology, string connectionString, QueueType queueType)
            : this(GetBuiltInTopology(topology), connectionString, queueType)
        {
        }

        /// <summary>
        /// Creates new instance of the RabbitMQ transport.
        /// </summary>
        /// <param name="topology">The custom topology to use.</param>
        /// <param name="connectionString">Connection string.</param>
        /// <param name="queueType">The type of queue to use for receiving queues.</param>
        public RabbitMQTransport(IRoutingTopology topology, string connectionString, QueueType queueType)
            : base(TransportTransactionMode.ReceiveOnly,
                supportsDelayedDelivery: true,
                supportsPublishSubscribe: true,
                supportsTTBR: queueType == QueueType.Classic)
        {
            Guard.AgainstNull(nameof(topology), topology);
            Guard.AgainstNull(nameof(connectionString), connectionString);

            QueueType = queueType;
            RoutingTopology = topology;
            ConnectionConfiguration = ConnectionConfiguration.Create(connectionString);

            InitializeClientCertificate();
        }

        /// <summary>
        /// Connection information parsed from the connection string
        /// </summary>
        internal ConnectionConfiguration ConnectionConfiguration { get; set; }

        /// <summary>
        ///     The routing topology to use. If not set the conventional routing topology will be used
        ///     <seealso cref="ConventionalRoutingTopology" />.
        /// </summary>
        public IRoutingTopology RoutingTopology { get; set; }

        /// <summary>
        ///     The strategy for deriving the message ID from the raw RabbitMQ message. Override in case of native integration when
        ///     the sender
        ///     of the message is not an NServiceBus endpoint.
        /// </summary>
        public Func<BasicDeliverEventArgs, string> MessageIdStrategy
        {
            get => messageIdStrategy;
            set
            {
                Guard.AgainstNull("value", value);
                messageIdStrategy = value;
            }
        }

        /// <summary>
        ///     Time to wait before triggering a circuit breaker that initiates the endpoint shutdown procedure when the
        ///     message pump's connection to the broker is lost and cannot be recovered.
        /// </summary>
        public TimeSpan TimeToWaitBeforeTriggeringCircuitBreaker
        {
            get => timeToWaitBeforeTriggeringCircuitBreaker;
            set
            {
                Guard.AgainstNegativeAndZero("value", value);
                timeToWaitBeforeTriggeringCircuitBreaker = value;
            }
        }

        /// <summary>
        ///     The calculation method for prefetch count. By default 3 times the maximum concurrency value.
        ///     The argument for the callback is the maximum concurrency. The result needs to be a positive integer value.
        /// </summary>
        public PrefetchCountCalculation PrefetchCountCalculation
        {
            get => prefetchCountCalculation;
            set
            {
                Guard.AgainstNull("value", value);
                prefetchCountCalculation = value;
            }
        }

        /// <summary>
        ///     The certificate to use for client authentication when connecting to the broker via TLS.
        /// </summary>
        public X509Certificate2 ClientCertificate { get; set; }

        /// <summary>
        ///     Should the client validate the broker certificate when connecting via TLS.
        /// </summary>
        public bool ValidateRemoteCertificate { get; set; } = true;

        /// <summary>
        ///     Specifies if an external authentication mechanism should be used for client authentication.
        /// </summary>
        public bool UseExternalAuthMechanism { get; set; } = false;

        /// <summary>
        ///     The interval for heartbeats between the endpoint and the broker.
        /// </summary>
        public TimeSpan? HeartbeatInterval
        {
            get => heartbeatIntervalOverride;
            set
            {
                Guard.AgainstNull("value", value);
                Guard.AgainstNegativeAndZero("value", value.Value);
                heartbeatIntervalOverride = value;
            }
        }
        /// <summary>
        ///     The time to wait between attempts to reconnect to the broker if the connection is lost.
        /// </summary>
        public TimeSpan? NetworkRecoveryInterval
        {
            get => networkRecoveryIntervalOverride;
            set
            {
                Guard.AgainstNull("value", value);
                Guard.AgainstNegativeAndZero("value", value.Value);
                networkRecoveryIntervalOverride = value;
            }
        }

        internal QueueType QueueType { get; }

        /// <summary>
        /// Adds a new node for use within a cluster.
        /// </summary>
        /// <param name="host">The hostname of the node.</param>
        /// <param name="port">The port of the node.</param>
        public void AddClusterNode(string host, int port = -1)
        {
            additionalHosts.Add((host, port));
        }

        /// <summary>
        ///     Initializes all the factories and supported features for the transport. This method is called right before all
        ///     features
        ///     are activated and the settings will be locked down. This means you can use the SettingsHolder both for providing
        ///     default capabilities as well as for initializing the transport's configuration based on those settings (the user
        ///     cannot
        ///     provide information anymore at this stage).
        /// </summary>
        public override Task<TransportInfrastructure> Initialize(HostSettings hostSettings,
            ReceiveSettings[] receivers, string[] sendingAddresses, CancellationToken cancellationToken = default)
        {
            ValidateAndApplyLegacyConfiguration();

            X509Certificate2Collection certCollection = null;

            if (ClientCertificate != null)
            {
                certCollection = new X509Certificate2Collection(ClientCertificate);
            }

            var connectionFactory = new ConnectionFactory(hostSettings.Name, ConnectionConfiguration, certCollection, !ValidateRemoteCertificate,
                UseExternalAuthMechanism, HeartbeatInterval, NetworkRecoveryInterval, additionalHosts);

            var channelProvider = new ChannelProvider(connectionFactory, NetworkRecoveryInterval ?? ConnectionConfiguration.RetryDelay, RoutingTopology);
            channelProvider.CreateConnection();

            var converter = new MessageConverter(MessageIdStrategy);

            var infra = new RabbitMQTransportInfrastructure(hostSettings, receivers, connectionFactory,
                RoutingTopology, channelProvider, converter, TimeToWaitBeforeTriggeringCircuitBreaker,
                PrefetchCountCalculation, NetworkRecoveryInterval ?? ConnectionConfiguration.RetryDelay);

            if (hostSettings.SetupInfrastructure)
            {
                infra.SetupInfrastructure(QueueType, sendingAddresses);
            }

            return Task.FromResult<TransportInfrastructure>(infra);
        }

        void InitializeClientCertificate()
        {
            if (!string.IsNullOrEmpty(ConnectionConfiguration.CertPath) && !string.IsNullOrEmpty(ConnectionConfiguration.CertPassphrase))
            {
                ClientCertificate = new X509Certificate2(ConnectionConfiguration.CertPath, ConnectionConfiguration.CertPassphrase);
            }
        }

        /// <summary>
        ///     Translates a <see cref="T:NServiceBus.Transport.QueueAddress" /> object into a transport specific queue
        ///     address-string.
        /// </summary>
        [ObsoleteEx(
            Message = "Inject the ITransportAddressResolver type to access the address translation mechanism at runtime. See the NServiceBus version 8 upgrade guide for further details.",
            TreatAsErrorFromVersion = "9",
            RemoveInVersion = "10")]
#pragma warning disable CS0672 // Member overrides obsolete member
        public override string ToTransportAddress(QueueAddress address)
#pragma warning restore CS0672 // Member overrides obsolete member
            => RabbitMQTransportInfrastructure.TranslateAddress(address);

        /// <summary>
        ///     Returns a list of all supported transaction modes of this transport.
        /// </summary>
        public override IReadOnlyCollection<TransportTransactionMode> GetSupportedTransactionModes() =>
            SupportedTransactionModes;

        internal static IRoutingTopology GetBuiltInTopology(Topology topology)
        {
            return topology == Topology.Conventional
                ? new ConventionalRoutingTopology(true)
                : new DirectRoutingTopology(true);
        }
    }
}