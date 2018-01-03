namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;
    using DelayedDelivery;
    using Features;
    using global::RabbitMQ.Client.Events;
    using Janitor;
    using Performance.TimeToBeReceived;
    using Routing;
    using Settings;

    [SkipWeaving]
    sealed class RabbitMQTransportInfrastructure : TransportInfrastructure, IDisposable
    {
        const string coreSendOnlyEndpointKey = "Endpoint.SendOnly";
        const string coreHostInformationDisplayNameKey = "NServiceBus.HostInformation.DisplayName";

        readonly SettingsHolder settings;
        readonly ConnectionFactory connectionFactory;
        readonly ChannelProvider channelProvider;
        IRoutingTopology routingTopology;

        public RabbitMQTransportInfrastructure(SettingsHolder settings, string connectionString)
        {
            this.settings = settings;

            var connectionConfiguration = ConnectionConfiguration.Create(connectionString, settings.EndpointName());

            X509CertificateCollection clientCertificates;
            settings.TryGet(SettingsKeys.ClientCertificates, out clientCertificates);

            bool disableRemoteCertificateValidation;
            settings.TryGet(SettingsKeys.DisableRemoteCertificateValidation, out disableRemoteCertificateValidation);

            bool useExternalAuthMechanism;
            settings.TryGet(SettingsKeys.UseExternalAuthMechanism, out useExternalAuthMechanism);

            connectionFactory = new ConnectionFactory(connectionConfiguration, clientCertificates, disableRemoteCertificateValidation, useExternalAuthMechanism);

            routingTopology = CreateRoutingTopology();

            var routingTopologySupportsDelayedDelivery = routingTopology is ISupportDelayedDelivery;
            settings.Set(SettingsKeys.RoutingTopologySupportsDelayedDelivery, routingTopologySupportsDelayedDelivery);

            if (routingTopologySupportsDelayedDelivery)
            {
                var timeoutManagerFeatureDisabled = settings.GetOrDefault<FeatureState>(typeof(TimeoutManager).FullName) == FeatureState.Disabled;
                var sendOnlyEndpoint = settings.GetOrDefault<bool>(coreSendOnlyEndpointKey);

                if (timeoutManagerFeatureDisabled || sendOnlyEndpoint)
                {
                    settings.Set(SettingsKeys.DisableTimeoutManager, true);
                }
            }

            bool usePublisherConfirms;
            if (!settings.TryGet(SettingsKeys.UsePublisherConfirms, out usePublisherConfirms))
            {
                usePublisherConfirms = true;
            }

            channelProvider = new ChannelProvider(connectionFactory, routingTopology, usePublisherConfirms);

            RequireOutboxConsent = false;
        }

        public override IEnumerable<Type> DeliveryConstraints
        {
            get
            {
                var constraints = new List<Type>
                {
                    typeof(DiscardIfNotReceivedBefore),
                    typeof(NonDurableDelivery)
                };

                if (settings.HasSetting(SettingsKeys.DisableTimeoutManager))
                {
                    constraints.Add(typeof(DoNotDeliverBefore));
                    constraints.Add(typeof(DelayDeliveryWith));
                }

                return constraints;
            }
        }

        public override OutboundRoutingPolicy OutboundRoutingPolicy => new OutboundRoutingPolicy(OutboundRoutingType.Unicast, OutboundRoutingType.Multicast, OutboundRoutingType.Unicast);

        public override TransportTransactionMode TransactionMode => TransportTransactionMode.ReceiveOnly;

        public override EndpointInstance BindToLocalEndpoint(EndpointInstance instance) => instance;

        public override TransportReceiveInfrastructure ConfigureReceiveInfrastructure()
        {
            return new TransportReceiveInfrastructure(
                    () => CreateMessagePump(),
                    () => new QueueCreator(connectionFactory, routingTopology, settings.DurableMessagesEnabled()),
                    () => Task.FromResult(ObsoleteAppSettings.Check()));
        }

        public override TransportSendInfrastructure ConfigureSendInfrastructure()
        {
            return new TransportSendInfrastructure(
                () => new MessageDispatcher(channelProvider),
                () => Task.FromResult(DelayInfrastructure.CheckForInvalidSettings(settings)));
        }

        public override TransportSubscriptionInfrastructure ConfigureSubscriptionInfrastructure()
        {
            return new TransportSubscriptionInfrastructure(() => new SubscriptionManager(connectionFactory, routingTopology, settings.LocalAddress()));
        }

        public override string ToTransportAddress(LogicalAddress logicalAddress)
        {
            var queue = new StringBuilder(logicalAddress.EndpointInstance.Endpoint);

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

        public override Task Start()
        {
            channelProvider.CreateConnection();

            return base.Start();
        }

        public void Dispose()
        {
            channelProvider.Dispose();
        }

        IRoutingTopology CreateRoutingTopology()
        {
            var durable = settings.DurableMessagesEnabled();
            Func<bool, IRoutingTopology> topologyFactory;

            if (!settings.TryGet(out topologyFactory))
            {
                topologyFactory = d => new ConventionalRoutingTopology(d);
            }

            return topologyFactory(durable);
        }

        IPushMessages CreateMessagePump()
        {
            MessageConverter messageConverter;

            if (settings.HasSetting(SettingsKeys.CustomMessageIdStrategy))
            {
                messageConverter = new MessageConverter(settings.Get<Func<BasicDeliverEventArgs, string>>(SettingsKeys.CustomMessageIdStrategy));
            }
            else
            {
                messageConverter = new MessageConverter();
            }

            string hostDisplayName;
            if (!settings.TryGet(coreHostInformationDisplayNameKey, out hostDisplayName))
            {
                hostDisplayName = Support.RuntimeEnvironment.MachineName;
            }

            var consumerTag = $"{hostDisplayName} - {settings.EndpointName()}";

            var queuePurger = new QueuePurger(connectionFactory);

            TimeSpan timeToWaitBeforeTriggeringCircuitBreaker;
            if (!settings.TryGet(SettingsKeys.TimeToWaitBeforeTriggeringCircuitBreaker, out timeToWaitBeforeTriggeringCircuitBreaker))
            {
                timeToWaitBeforeTriggeringCircuitBreaker = TimeSpan.FromMinutes(2);
            }

            int prefetchMultiplier;
            if (!settings.TryGet(SettingsKeys.PrefetchMultiplier, out prefetchMultiplier))
            {
                prefetchMultiplier = 3;
            }

            ushort prefetchCount;
            if (!settings.TryGet(SettingsKeys.PrefetchCount, out prefetchCount))
            {
                prefetchCount = 0;
            }

            return new MessagePump(connectionFactory, messageConverter, consumerTag, channelProvider, queuePurger, timeToWaitBeforeTriggeringCircuitBreaker, prefetchMultiplier, prefetchCount);
        }
    }
}
