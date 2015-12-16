﻿namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Janitor;
    using NServiceBus.Performance.TimeToBeReceived;
    using NServiceBus.Settings;
    using NServiceBus.Support;
    using NServiceBus.Transports;
    using NServiceBus.Transports.RabbitMQ;
    using NServiceBus.Transports.RabbitMQ.Config;
    using NServiceBus.Transports.RabbitMQ.Connection;
    using NServiceBus.Transports.RabbitMQ.Routing;
    using RabbitMQ.Client.Events;

    /// <summary>
    /// Transport definition for RabbirtMQ
    /// </summary>
    [SkipWeaving]
    public class RabbitMQTransport : TransportDefinition, IDisposable
    {
        /// <summary>
        /// Ctor
        /// </summary>
        public RabbitMQTransport()
        {
            RequireOutboxConsent = false;
        }

        /// <summary>
        /// Gets an example connection string to use when reporting lack of configured connection string to the user.
        /// </summary>
        public override string ExampleConnectionStringForErrorMessage => "host=localhost";

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            connectionManager.Dispose();
        }

        /// <summary>
        /// Configures transport for receiving.
        /// </summary>
        protected override TransportReceivingConfigurationResult ConfigureForReceiving(TransportReceivingConfigurationContext context)
        {
            Initialize(context.Settings, context.ConnectionString);

            return new TransportReceivingConfigurationResult(
                () =>
                {
                    var callbacks = new Callbacks(context.Settings);
                    
                    MessageConverter messageConverter;

                    if (context.Settings.HasSetting(CustomMessageIdStrategy))
                    {
                        messageConverter = new MessageConverter(context.Settings.Get<Func<BasicDeliverEventArgs, string>>(CustomMessageIdStrategy));
                    }
                    else
                    {
                        messageConverter = new MessageConverter();
                    }

                    string hostDisplayName;
                    if (!context.Settings.TryGet("NServiceBus.HostInformation.DisplayName", out hostDisplayName)) //this was added in 5.1.2 of the core
                    {
                        hostDisplayName = RuntimeEnvironment.MachineName;
                    }

                    var consumerTag = $"{hostDisplayName} - {context.Settings.EndpointName()}";

                    var receiveOptions = new ReceiveOptions(workQueue =>
                    {
                        //if this isn't the main queue we shouldn't use callback receiver
                        if (!callbacks.IsEnabledFor(workQueue))
                        {
                            return SecondaryReceiveSettings.Disabled();
                        }
                        return SecondaryReceiveSettings.Enabled(callbacks.QueueAddress, callbacks.MaxConcurrency);
                    },
                        messageConverter,
                        connectionConfiguration.PrefetchCount,
                        connectionConfiguration.DequeueTimeout*1000,
                        context.Settings.GetOrDefault<bool>("Transport.PurgeOnStartup"),
                        consumerTag);

                    var provider = new ChannelProvider(connectionManager, false, connectionConfiguration.MaxWaitTimeForConfirms);

                    return new RabbitMqMessagePump(connectionManager, topology, provider, receiveOptions);
                },
                () => new RabbitMqQueueCreator(connectionManager, topology, context.Settings),
                () => Task.FromResult(StartupCheckResult.Success));
        }

        /// <summary>
        /// Configures transport for sending.
        /// </summary>
        protected override TransportSendingConfigurationResult ConfigureForSending(TransportSendingConfigurationContext context)
        {
            Initialize(context.Settings, context.ConnectionString);

            var callbacks = new Callbacks(context.Settings);
            
            var provider = new ChannelProvider(connectionManager, connectionConfiguration.UsePublisherConfirms, connectionConfiguration.MaxWaitTimeForConfirms);

            return new TransportSendingConfigurationResult(() => new RabbitMqMessageSender(topology, provider, callbacks), () => Task.FromResult(StartupCheckResult.Success));
        }

        private void Initialize(ReadOnlySettings settings, string connectionString)
        {
            CreateTopology(settings);
            CreateConnectionConfiguration(settings, connectionString);
            CreateConnectionManager();
        }

        private void CreateConnectionManager()
        {
            if (connectionManager != null)
            {
                return;
            }

            connectionManager = new RabbitMqConnectionManager(new RabbitMqConnectionFactory(connectionConfiguration), connectionConfiguration);
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
        /// Returns the list of supported delivery constraints for this transport.
        /// </summary>
        public override IEnumerable<Type> GetSupportedDeliveryConstraints()
        {
            yield return typeof(DiscardIfNotReceivedBefore);
            yield return typeof(NonDurableDelivery);
        }

        /// <summary>
        /// Gets the highest supported transaction mode for the this transport.
        /// </summary>
        public override TransportTransactionMode GetSupportedTransactionMode()
        {
            return TransportTransactionMode.ReceiveOnly;
        }

        /// <summary>
        /// Will be called if the transport has indicated that it has native support for pub sub.
        /// Creates a transport address for the input queue defined by a logical address.
        /// </summary>
        public override IManageSubscriptions GetSubscriptionManager()
        {
            return new RabbitMqSubscriptionManager(connectionManager, topology, localQueue);
        }

        /// <summary>
        /// Returns the discriminator for this endpoint instance.
        /// </summary>
        public override string GetDiscriminatorForThisEndpointInstance(ReadOnlySettings settings)
        {
            return null;
        }

        /// <summary>
        /// Converts a given logical address to the transport address.
        /// </summary>
        /// <param name="logicalAddress">The logical address.</param>
        /// <returns>
        /// The transport address.
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

            localQueue = queue.ToString();

            return localQueue;
        }

        /// <summary>
        /// Returns the outbound routing policy selected for the transport.
        /// </summary>
        public override OutboundRoutingPolicy GetOutboundRoutingPolicy(ReadOnlySettings settings)
        {
            return new OutboundRoutingPolicy(OutboundRoutingType.Unicast, OutboundRoutingType.Multicast, OutboundRoutingType.Unicast);
        }

        private ConnectionConfiguration connectionConfiguration;
        private RabbitMqConnectionManager connectionManager;
        private string localQueue;
        private IRoutingTopology topology;
        internal const string CustomMessageIdStrategy = "RabbitMQ.CustomMessageIdStrategy";
    }
}