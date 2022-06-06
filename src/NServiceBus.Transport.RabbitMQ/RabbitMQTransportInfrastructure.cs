namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;
    using DelayedDelivery;
    using global::RabbitMQ.Client.Events;
    using Performance.TimeToBeReceived;
    using Routing;
    using Settings;

    sealed class RabbitMQTransportInfrastructure : TransportInfrastructure
    {
        const string coreHostInformationDisplayNameKey = "NServiceBus.HostInformation.DisplayName";

        readonly SettingsHolder settings;
        readonly TimeSpan networkRetryDelay;
        readonly ConnectionFactory connectionFactory;
        readonly IRoutingTopology routingTopology;
        readonly ChannelProvider channelProvider;

        public RabbitMQTransportInfrastructure(SettingsHolder settings, string connectionString)
        {
            this.settings = settings;

            var endpointName = settings.EndpointName();
            var connectionConfiguration = ConnectionConfiguration.Create(connectionString);

            settings.TryGet(SettingsKeys.ClientCertificateCollection, out X509Certificate2Collection clientCertificateCollection);
            settings.TryGet(SettingsKeys.DisableRemoteCertificateValidation, out bool disableRemoteCertificateValidation);
            settings.TryGet(SettingsKeys.UseExternalAuthMechanism, out bool useExternalAuthMechanism);
            settings.TryGet(SettingsKeys.HeartbeatInterval, out TimeSpan? heartbeatInterval);
            settings.TryGet(SettingsKeys.NetworkRecoveryInterval, out TimeSpan? networkRecoveryInterval);
            networkRetryDelay = networkRecoveryInterval ?? connectionConfiguration.RetryDelay;

            connectionFactory = new ConnectionFactory(endpointName, connectionConfiguration, clientCertificateCollection, disableRemoteCertificateValidation, useExternalAuthMechanism, heartbeatInterval, networkRecoveryInterval);

            routingTopology = CreateRoutingTopology();

            channelProvider = new ChannelProvider(connectionFactory, networkRetryDelay, routingTopology);
        }

        public override IEnumerable<Type> DeliveryConstraints => new List<Type> { typeof(DiscardIfNotReceivedBefore), typeof(NonDurableDelivery), typeof(DoNotDeliverBefore), typeof(DelayDeliveryWith) };

        public override OutboundRoutingPolicy OutboundRoutingPolicy => new OutboundRoutingPolicy(OutboundRoutingType.Unicast, OutboundRoutingType.Multicast, OutboundRoutingType.Unicast);

        public override TransportTransactionMode TransactionMode => TransportTransactionMode.ReceiveOnly;

        public override EndpointInstance BindToLocalEndpoint(EndpointInstance instance) => instance;

        public override TransportReceiveInfrastructure ConfigureReceiveInfrastructure()
        {
            return new TransportReceiveInfrastructure(
                    () => CreateMessagePump(),
                    () => new QueueCreator(connectionFactory, routingTopology),
                    () => Task.FromResult(StartupCheckResult.Success));
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

        public override Task Stop()
        {
            channelProvider.Dispose();
            return base.Stop();
        }

        IRoutingTopology CreateRoutingTopology()
        {
            if (!settings.TryGet(out Func<bool, IRoutingTopology> topologyFactory))
            {
                throw new InvalidOperationException("A routing topology must be configured with one of the 'EndpointConfiguration.UseTransport<RabbitMQTransport>().UseXXXXRoutingTopology()` methods.");
            }

            if (!settings.TryGet(SettingsKeys.UseDurableExchangesAndQueues, out bool useDurableExchangesAndQueues))
            {
                useDurableExchangesAndQueues = true;
            }

            return topologyFactory(useDurableExchangesAndQueues);
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

            if (!settings.TryGet(coreHostInformationDisplayNameKey, out string hostDisplayName))
            {
                hostDisplayName = Support.RuntimeEnvironment.MachineName;
            }

            var consumerTag = $"{hostDisplayName} - {settings.EndpointName()}";

            var queuePurger = new QueuePurger(connectionFactory);

            if (!settings.TryGet(SettingsKeys.TimeToWaitBeforeTriggeringCircuitBreaker, out TimeSpan timeToWaitBeforeTriggeringCircuitBreaker))
            {
                timeToWaitBeforeTriggeringCircuitBreaker = TimeSpan.FromMinutes(2);
            }

            if (!settings.TryGet(SettingsKeys.PrefetchMultiplier, out int prefetchMultiplier))
            {
                prefetchMultiplier = 3;
            }

            if (!settings.TryGet(SettingsKeys.PrefetchCount, out ushort prefetchCount))
            {
                prefetchCount = 0;
            }

            return new MessagePump(connectionFactory, messageConverter, consumerTag, channelProvider, queuePurger, timeToWaitBeforeTriggeringCircuitBreaker, prefetchMultiplier, prefetchCount, networkRetryDelay);
        }
    }
}
