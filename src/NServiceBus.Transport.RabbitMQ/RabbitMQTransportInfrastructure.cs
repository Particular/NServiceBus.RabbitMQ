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

            settings.TryGet(SettingsKeys.ClientCertificates, out X509CertificateCollection clientCertificates);
            settings.TryGet(SettingsKeys.DisableRemoteCertificateValidation, out bool disableRemoteCertificateValidation);
            settings.TryGet(SettingsKeys.UseExternalAuthMechanism, out bool useExternalAuthMechanism);
            connectionFactory = new ConnectionFactory(connectionConfiguration, clientCertificates, disableRemoteCertificateValidation, useExternalAuthMechanism);

            routingTopology = CreateRoutingTopology();

            if (!settings.TryGet(SettingsKeys.UsePublisherConfirms, out bool usePublisherConfirms))
            {
                usePublisherConfirms = true;
            }

            channelProvider = new ChannelProvider(connectionFactory, connectionConfiguration.RetryDelay, routingTopology, usePublisherConfirms);
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

                if (!settings.HasSetting(SettingsKeys.EnableTimeoutManager))
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
                if (!settings.DurableMessagesEnabled())
                {
                    throw new Exception("When durable messages are disabled, 'EndpointConfiguration.UseTransport<RabbitMQTransport>().UseDurableExchangesAndQueues()' must also be called to specify exchange and queue durability settings.");
                }

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

            return new MessagePump(connectionFactory, messageConverter, consumerTag, channelProvider, queuePurger, timeToWaitBeforeTriggeringCircuitBreaker, prefetchMultiplier, prefetchCount);
        }
    }
}
