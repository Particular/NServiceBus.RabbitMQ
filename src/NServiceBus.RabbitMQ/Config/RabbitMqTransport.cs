namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using NServiceBus.Settings;
    using NServiceBus.Transports;
    using NServiceBus.Transports.RabbitMQ;
    using NServiceBus.Transports.RabbitMQ.Config;
    using NServiceBus.Transports.RabbitMQ.Connection;
    using NServiceBus.Transports.RabbitMQ.Routing;

    /// <summary>
    ///     Transport definition for RabbirtMQ
    /// </summary>
    public class RabbitMQTransport : TransportDefinition
    {
        private ConnectionConfiguration connectionConfiguration;
        private IManageRabbitMqConnections connectionManager;
        private IRoutingTopology topology;

        /// <summary>
        ///     Ctor
        /// </summary>
        public RabbitMQTransport()
        {
            RequireOutboxConsent = false;
        }

        /// <summary>
        ///     Gets an example connection string to use when reporting lack of configured connection string to the user.
        /// </summary>
        public override string ExampleConnectionStringForErrorMessage => "host=localhost";

        /// <summary>
        ///     Configures transport for receiving.
        /// </summary>
        protected override TransportReceivingConfigurationResult ConfigureForReceiving(TransportReceivingConfigurationContext context)
        {
            Initialize(context.Settings, context.ConnectionString);

            return new TransportReceivingConfigurationResult(
                () =>
                {
                    context.Pipeline.Register<OpenPublishChannelBehavior.Registration>();
                    context.Pipeline.Register<ReadIncomingCallbackAddressBehavior.Registration>();

                    return new FakePusher();
                },
                () => new RabbitMqQueueCreator(connectionManager, topology, context.Settings),
                () => Task.FromResult(StartupCheckResult.Success));
        }

        /// <summary>
        ///     Configures transport for sending.
        /// </summary>
        protected override TransportSendingConfigurationResult ConfigureForSending(TransportSendingConfigurationContext context)
        {
            Initialize(context.Settings, context.ConnectionString);

            var provider = new ChannelProvider(connectionManager, connectionConfiguration.UsePublisherConfirms, connectionConfiguration.MaxWaitTimeForConfirms);

            return new TransportSendingConfigurationResult(() => new RabbitMqMessageSender(topology, provider), () => Task.FromResult(StartupCheckResult.Success));
        }

        private void CreateConnectionManager()
        {
            if (connectionManager != null)
            {
                return;
            }

            connectionManager = new RabbitMqConnectionManager(new RabbitMqConnectionFactory(connectionConfiguration), connectionConfiguration);
        }

        private void Initialize(ReadOnlySettings settings, string connectionString)
        {
            CreateTopology(settings);
            CreateConnectionConfiguration(settings, connectionString);
            CreateConnectionManager();
        }

        private void CreateConnectionConfiguration(ReadOnlySettings settings, string connectionString)
        {
            if (connectionConfiguration != null)
            {
                return;
            }

            connectionConfiguration = new ConnectionStringParser(settings).Parse(connectionString);
        }

        private void CreateTopology(ReadOnlySettings settings)
        {
            if (topology != null)
            {
                return;
            }

            if (settings.HasSetting<IRoutingTopology>())
            {
                topology = settings.Get<IRoutingTopology>();
            }
            else
            {
                var durable = GetDurableMessagesEnabled(settings);

                DirectRoutingTopology.Conventions conventions;

                if (settings.TryGet(out conventions))
                {
                    topology = new DirectRoutingTopology(conventions, durable);
                }
                else
                {
                    topology = new ConventionalRoutingTopology(durable);
                }
            }
        }

        private static bool GetDurableMessagesEnabled(ReadOnlySettings settings)
        {
            bool durableMessagesEnabled;
            if (settings.TryGet("Endpoint.DurableMessages", out durableMessagesEnabled))
            {
                return durableMessagesEnabled;
            }
            return true;
        }

        /// <summary>
        ///     Returns the list of supported delivery constraints for this transport.
        /// </summary>
        public override IEnumerable<Type> GetSupportedDeliveryConstraints()
        {
            yield break;
        }

        /// <summary>
        ///     Gets the highest supported transaction mode for the this transport.
        /// </summary>
        public override TransportTransactionMode GetSupportedTransactionMode()
        {
            return TransportTransactionMode.ReceiveOnly;
        }

        /// <summary>
        ///     Will be called if the transport has indicated that it has native support for pub sub.
        ///     Creates a transport address for the input queue defined by a logical address.
        /// </summary>
        public override IManageSubscriptions GetSubscriptionManager()
        {
            return new RabbitMqSubscriptionManager();
        }

        /// <summary>
        ///     Returns the discriminator for this endpoint instance.
        /// </summary>
        public override string GetDiscriminatorForThisEndpointInstance(ReadOnlySettings settings)
        {
            return null;
        }

        /// <summary>
        ///     Converts a given logical address to the transport address.
        /// </summary>
        /// <param name="logicalAddress">The logical address.</param>
        /// <returns>
        ///     The transport address.
        /// </returns>
        public override string ToTransportAddress(LogicalAddress logicalAddress)
        {
            var queue = new StringBuilder(logicalAddress.EndpointInstance.Endpoint.ToString());

            if (logicalAddress.EndpointInstance.UserDiscriminator != null)
            {
                queue.Append("-" + logicalAddress.EndpointInstance.UserDiscriminator);
            }
            if (logicalAddress.Qualifier != null)
            {
                queue.Append("." + logicalAddress.Qualifier);
            }

            return queue.ToString();
        }

        /// <summary>
        ///     Returns the outbound routing policy selected for the transport.
        /// </summary>
        public override OutboundRoutingPolicy GetOutboundRoutingPolicy(ReadOnlySettings settings)
        {
            return new OutboundRoutingPolicy(OutboundRoutingType.Unicast, OutboundRoutingType.Multicast, OutboundRoutingType.Unicast);
        }

        private class FakePusher : IPushMessages
        {
            /// <summary>
            ///     Initializes the <see cref="T:NServiceBus.Transports.IPushMessages" />.
            /// </summary>
            public Task Init(Func<PushContext, Task> pipe, CriticalError criticalError, PushSettings settings)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            ///     Starts pushing message/&gt;.
            /// </summary>
            public void Start(PushRuntimeSettings limitations)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            ///     Stops pushing messages.
            /// </summary>
            public Task Stop()
            {
                throw new NotImplementedException();
            }
        }
    }
}