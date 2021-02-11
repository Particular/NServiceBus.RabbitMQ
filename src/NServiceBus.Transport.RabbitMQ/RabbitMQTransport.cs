namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;
    using RabbitMQ.Client;
    using RabbitMQ.Client.Events;
    using Transport;
    using Transport.RabbitMQ;
    using ConnectionFactory = Transport.RabbitMQ.ConnectionFactory;

    /// <summary>
    ///     Transport definition for RabbitMQ.
    /// </summary>
    public class RabbitMQTransport : TransportDefinition
    {
        static readonly TransportTransactionMode[] supportedTransactionModes =
        {
            TransportTransactionMode.None, TransportTransactionMode.ReceiveOnly
        };

        TimeSpan heartbeatInterval = TimeSpan.FromMinutes(1);
        string host;
        Func<BasicDeliverEventArgs, string> messageIdStrategy = MessageConverter.DefaultMessageIdStrategy;
        TimeSpan networkRecoveryInterval = TimeSpan.FromSeconds(10);
        Func<int, int> prefetchCountCalculation = maxConcurrency => 3 * maxConcurrency;
        IRoutingTopology routingTopology = new ConventionalRoutingTopology(true);

        TimeSpan timeToWaitBeforeTriggeringCircuitBreaker = TimeSpan.FromMinutes(2);

        /// <summary>
        ///     Creates new instance of the RabbitMQ transport.
        /// </summary>
        public RabbitMQTransport(string connectionString)
            : base(TransportTransactionMode.ReceiveOnly, true, true, true)
        {
            if (connectionString.StartsWith("amqp", StringComparison.OrdinalIgnoreCase))
            {
                AmqpConnectionString.Parse(connectionString)(this);
            }
            else
            {
                NServiceBusConnectionString.Parse(connectionString)(this);
            }
        }

        /// <summary>
        ///     Creates new instance of the RabbitMQ transport.
        /// </summary>
        public RabbitMQTransport()
            : base(TransportTransactionMode.ReceiveOnly, true, true, true)
        {
        }

        /// <summary>
        ///     The host to connect to.
        /// </summary>
        public string Host
        {
            get => host;
            set
            {
                Guard.AgainstNullAndEmpty("value", value);
                host = value;
            }
        }

        /// <summary>
        ///     The port to connect to.
        ///     If not specified, the default port will be used (5672 if not encrypted and 5671 if using TLS)
        /// </summary>
        public int? Port { get; set; }

        /// <summary>
        ///     The vhost to connect to.
        /// </summary>
        public string VHost { get; set; } = "/";

        /// <summary>
        ///     The user name to pass to the broker for authentication.
        /// </summary>
        public string UserName { get; set; } = "guest";

        /// <summary>
        ///     The password to pass to the broker for authentication.
        /// </summary>
        public string Password { get; set; } = "guest";

        /// <summary>
        ///     The routing topology to use. If not set the conventional routing topology will be used
        ///     <seealso cref="ConventionalRoutingTopology" />.
        /// </summary>
        public IRoutingTopology RoutingTopology
        {
            get => routingTopology;
            set
            {
                Guard.AgainstNull("value", value);
                routingTopology = value;
            }
        }

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
        public Func<int, int> PrefetchCountCalculation
        {
            get => prefetchCountCalculation;
            set
            {
                Guard.AgainstNull("value", value);
                prefetchCountCalculation = value;
            }
        }

        /// <summary>
        ///     Configures if the client should use TLS-secured connection.
        /// </summary>
        public bool UseTLS { get; set; }

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
        public TimeSpan HeartbeatInterval
        {
            get => heartbeatInterval;
            set
            {
                Guard.AgainstNegativeAndZero("value", value);
                heartbeatInterval = value;
            }
        }

        /// <summary>
        ///     The time to wait between attempts to reconnect to the broker if the connection is lost.
        /// </summary>
        public TimeSpan NetworkRecoveryInterval
        {
            get => networkRecoveryInterval;
            set
            {
                Guard.AgainstNegativeAndZero("value", value);
                networkRecoveryInterval = value;
            }
        }

        int DefaultPort => UseTLS ? 5671 : 5672;

        /// <summary>
        ///     Initializes all the factories and supported features for the transport. This method is called right before all
        ///     features
        ///     are activated and the settings will be locked down. This means you can use the SettingsHolder both for providing
        ///     default capabilities as well as for initializing the transport's configuration based on those settings (the user
        ///     cannot
        ///     provide information anymore at this stage).
        /// </summary>
        public override async Task<TransportInfrastructure> Initialize(HostSettings hostSettings,
            ReceiveSettings[] receivers, string[] sendingAddresses)
        {
            X509Certificate2Collection certCollection = null;
            if (ClientCertificate != null)
            {
                certCollection = new X509Certificate2Collection(ClientCertificate);
            }

            var connectionFactory = new ConnectionFactory(hostSettings.Name, Host, Port ?? DefaultPort,
                VHost, UserName, Password, UseTLS, certCollection, ValidateRemoteCertificate,
                UseExternalAuthMechanism, HeartbeatInterval, NetworkRecoveryInterval);

            var channelProvider = new ChannelProvider(connectionFactory, NetworkRecoveryInterval, routingTopology);
            channelProvider.CreateConnection();

            var converter = new MessageConverter(MessageIdStrategy);

            if (hostSettings.SetupInfrastructure)
            {
                string[] receivingAddresses = receivers.Select(x => x.ReceiveAddress).ToArray();
                await SetupInfrastructure(receivingAddresses, sendingAddresses, connectionFactory)
                    .ConfigureAwait(false);
            }

            return new RabbitMQTransportInfrastructure(hostSettings, receivers, connectionFactory,
                routingTopology, channelProvider, converter, TimeToWaitBeforeTriggeringCircuitBreaker,
                PrefetchCountCalculation);
        }

        Task SetupInfrastructure(string[] receivingQueues, string[] sendingQueues, ConnectionFactory connectionFactory)
        {
            using (IConnection connection = connectionFactory.CreateAdministrationConnection())
            using (IModel channel = connection.CreateModel())
            {
                DelayInfrastructure.Build(channel);

                routingTopology.Initialize(channel, receivingQueues, sendingQueues);

                foreach (string receivingAddress in receivingQueues)
                {
                    routingTopology.BindToDelayInfrastructure(channel, receivingAddress,
                        DelayInfrastructure.DeliveryExchange, DelayInfrastructure.BindingKey(receivingAddress));
                }
            }

            return Task.CompletedTask;
        }


        /// <summary>
        ///     Translates a <see cref="T:NServiceBus.Transport.QueueAddress" /> object into a transport specific queue
        ///     address-string.
        /// </summary>
        public override string ToTransportAddress(QueueAddress address)
        {
            var queue = new StringBuilder(address.BaseAddress);
            if (address.Discriminator != null)
            {
                queue.Append("-" + address.Discriminator);
            }

            if (address.Qualifier != null)
            {
                queue.Append("." + address.Qualifier);
            }

            return queue.ToString();
        }

        /// <summary>
        ///     Returns a list of all supported transaction modes of this transport.
        /// </summary>
        public override IReadOnlyCollection<TransportTransactionMode> GetSupportedTransactionModes() =>
            supportedTransactionModes;
    }
}