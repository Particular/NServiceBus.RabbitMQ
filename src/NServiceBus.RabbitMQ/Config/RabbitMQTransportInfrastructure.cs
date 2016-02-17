namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Janitor;
    using NServiceBus.Performance.TimeToBeReceived;
    using NServiceBus.Routing;
    using NServiceBus.Settings;
    using NServiceBus.Transports;
    using NServiceBus.Transports.RabbitMQ;
    using NServiceBus.Transports.RabbitMQ.Config;
    using NServiceBus.Transports.RabbitMQ.Connection;
    using NServiceBus.Transports.RabbitMQ.Routing;
    using RabbitMQ.Client.Events;

    /// <summary>
    /// Transport infrastructure definitions.
    /// </summary>
    [SkipWeaving]
    public class RabbitMQTransportInfrastructure : TransportInfrastructure, IDisposable
    {
        internal const string CustomMessageIdStrategy = "RabbitMQ.CustomMessageIdStrategy";

        readonly SettingsHolder settings;
        readonly ConnectionConfiguration connectionConfiguration;
        readonly RabbitMqConnectionManager connectionManager;
        IRoutingTopology topology;

        /// <summary>
        /// Creates an instance of the transport infrastructure.
        /// </summary>
        /// <param name="settings">An instance of the current settings.</param>
        /// <param name="connectionString">The connection string.</param>
        public RabbitMQTransportInfrastructure(SettingsHolder settings, string connectionString)
        {
            this.settings = settings;

            connectionConfiguration = new ConnectionStringParser(settings).Parse(connectionString);
            connectionManager = new RabbitMqConnectionManager(new RabbitMqConnectionFactory(connectionConfiguration));

            CreateTopology();

            RequireOutboxConsent = false;
        }

        /// <summary>
        /// Returns the list of supported delivery constraints for this transport.
        /// </summary>
        public override IEnumerable<Type> DeliveryConstraints => new[] { typeof(DiscardIfNotReceivedBefore), typeof(NonDurableDelivery) };

        /// <summary>
        /// Returns the outbound routing policy selected for the transport.
        /// </summary>
        public override OutboundRoutingPolicy OutboundRoutingPolicy => new OutboundRoutingPolicy(OutboundRoutingType.Unicast, OutboundRoutingType.Multicast, OutboundRoutingType.Unicast);

        /// <summary>
        /// Gets the highest supported transaction mode for the transport.
        /// </summary>
        public override TransportTransactionMode TransactionMode => TransportTransactionMode.ReceiveOnly;

        /// <summary>
        /// Returns the discriminator for this endpoint instance.
        /// </summary>
        public override EndpointInstance BindToLocalEndpoint(EndpointInstance instance) => instance;

        /// <summary>
        /// Gets the factories to receive messages.
        /// </summary>
        public override TransportReceiveInfrastructure ConfigureReceiveInfrastructure()
        {
            return new TransportReceiveInfrastructure(
                    () => CreateMessagePump(),
                    () => new RabbitMqQueueCreator(connectionManager, topology, settings.DurableMessagesEnabled()),
                    () => Task.FromResult(StartupCheckResult.Success));
        }

        /// <summary>
        /// Gets the factories to send messages.
        /// </summary>
        public override TransportSendInfrastructure ConfigureSendInfrastructure()
        {
            var provider = new ChannelProvider(connectionManager, connectionConfiguration.UsePublisherConfirms, connectionConfiguration.MaxWaitTimeForConfirms);

            return new TransportSendInfrastructure(
                () => new RabbitMqMessageSender(topology, provider),
                () => Task.FromResult(StartupCheckResult.Success));
        }

        /// <summary>
        /// Gets the factory to manage subscriptions.
        /// </summary>
        public override TransportSubscriptionInfrastructure ConfigureSubscriptionInfrastructure()
        {
            return new TransportSubscriptionInfrastructure(() => new RabbitMqSubscriptionManager(connectionManager, topology, settings.EndpointName().ToString()));
        }

        /// <summary>
        /// Converts a given logical address to the transport address.
        /// </summary>
        /// <param name="logicalAddress">The logical address.</param>
        /// <returns>The transport address.</returns>
        public override string ToTransportAddress(LogicalAddress logicalAddress)
        {
            var queue = new StringBuilder(logicalAddress.EndpointInstance.Endpoint.ToString());

            if (logicalAddress.EndpointInstance.Discriminator != null)
            {
                queue.Append("-" + logicalAddress.EndpointInstance.Discriminator);
            }

            if (logicalAddress.Qualifier != null)
            {
                queue.Append("." + logicalAddress.Qualifier);
            }

            return queue.ToString();
        }

        /// <summary>
        /// Disposes the connections managed by the infrastructure.
        /// </summary>
        public void Dispose()
        {
            connectionManager.Dispose();
        }

        void CreateTopology()
        {
            if (settings.HasSetting<IRoutingTopology>())
            {
                topology = settings.Get<IRoutingTopology>();
            }
            else
            {
                var durable = settings.DurableMessagesEnabled();

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

        IPushMessages CreateMessagePump()
        {
            MessageConverter messageConverter;

            if (settings.HasSetting(CustomMessageIdStrategy))
            {
                messageConverter = new MessageConverter(settings.Get<Func<BasicDeliverEventArgs, string>>(CustomMessageIdStrategy));
            }
            else
            {
                messageConverter = new MessageConverter();
            }

            string hostDisplayName;
            if (!settings.TryGet("NServiceBus.HostInformation.DisplayName", out hostDisplayName)) //this was added in 5.1.2 of the core
            {
                hostDisplayName = Support.RuntimeEnvironment.MachineName;
            }

            var consumerTag = $"{hostDisplayName} - {settings.EndpointName()}";

            var receiveOptions = new ReceiveOptions(messageConverter, settings.GetOrDefault<bool>("Transport.PurgeOnStartup"), consumerTag);

            var provider = new ChannelProvider(connectionManager, false, connectionConfiguration.MaxWaitTimeForConfirms);
            var queuePurger = new QueuePurger(connectionManager);
            var poisonMessageForwarder = new PoisonMessageForwarder(provider, topology);

            return new MessagePump(receiveOptions, connectionConfiguration, poisonMessageForwarder, queuePurger);
        }
    }
}
