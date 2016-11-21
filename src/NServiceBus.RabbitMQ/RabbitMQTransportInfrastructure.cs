namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client.Events;
    using Janitor;
    using Performance.TimeToBeReceived;
    using Routing;
    using Settings;

    [SkipWeaving]
    class RabbitMQTransportInfrastructure : TransportInfrastructure, IDisposable
    {
        readonly SettingsHolder settings;
        readonly ConnectionFactory connectionFactory;
        readonly ChannelProvider channelProvider;
        IRoutingTopology routingTopology;

        public RabbitMQTransportInfrastructure(SettingsHolder settings, string connectionString)
        {
            this.settings = settings;

            new ConnectionStringParser(settings).Parse(connectionString);
            connectionFactory = new ConnectionFactory(settings);

            routingTopology = CreateRoutingTopology();

            var usePublisherConfirms = settings.Get<bool>(SettingsKeys.UsePublisherConfirms);
            
            channelProvider = new ChannelProvider(connectionFactory, routingTopology, usePublisherConfirms);

            RequireOutboxConsent = false;
        }

        public override IEnumerable<Type> DeliveryConstraints => new[] { typeof(DiscardIfNotReceivedBefore), typeof(NonDurableDelivery) };

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
                () => Task.FromResult(StartupCheckResult.Success));
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

            string hostDisplayName = settings.Get<string>("NServiceBus.HostInformation.DisplayName");
            var consumerTag = $"{hostDisplayName} - {settings.EndpointName()}";

            var queuePurger = new QueuePurger(connectionFactory);

            var timeToWaitBeforeTriggeringCircuitBreaker = settings.Get<TimeSpan>(SettingsKeys.TimeToWaitBeforeTriggeringCircuitBreaker);
            var prefetchMultiplier = settings.Get<int>(SettingsKeys.PrefetchMultiplier);
            var prefetchCount = settings.Get<ushort>(SettingsKeys.PrefetchCount);

            return new MessagePump(connectionFactory, messageConverter, consumerTag, channelProvider, queuePurger, timeToWaitBeforeTriggeringCircuitBreaker, prefetchMultiplier, prefetchCount);
        }
    }
}
