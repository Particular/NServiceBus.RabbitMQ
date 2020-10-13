namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Text;
    using System.Threading.Tasks;

    sealed class RabbitMQTransportInfrastructure : TransportInfrastructure
    {
        readonly TransportSettings settings;
        private readonly RabbitMQTransport rabbitSettings;
        readonly ConnectionFactory connectionFactory;
        readonly ChannelProvider channelProvider;
        readonly IRoutingTopology routingTopology;

        public RabbitMQTransportInfrastructure(TransportSettings settings, RabbitMQTransport rabbitSettings)
        {
            this.settings = settings;
            this.rabbitSettings = rabbitSettings;

            var endpointName = this.settings.EndpointName.Name;
            var connectionConfiguration = ConnectionConfiguration.Create(rabbitSettings.ConnectionString, endpointName);

            connectionFactory = new ConnectionFactory(endpointName, connectionConfiguration, rabbitSettings.X509Certificate2Collection, rabbitSettings.DisableRemoteCertificateValidation, rabbitSettings.UseExternalAuthMechanism, rabbitSettings.HeartbeatInterval, rabbitSettings.NetworkRecoveryInterval);

            routingTopology = CreateRoutingTopology();

            channelProvider = new ChannelProvider(connectionFactory, connectionConfiguration.RetryDelay, routingTopology);
        }

        public override TransportTransactionMode TransactionMode => TransportTransactionMode.ReceiveOnly;

        public override TransportReceiveInfrastructure ConfigureReceiveInfrastructure(ReceiveSettings receiveSettings)
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
                () => Task.FromResult(StartupCheckResult.Success));
        }

        public override TransportSubscriptionInfrastructure ConfigureSubscriptionInfrastructure(SubscriptionSettings subscriptionSettings)
        {
            return new TransportSubscriptionInfrastructure(() => new SubscriptionManager(connectionFactory, routingTopology, subscriptionSettings.LocalAddress));
        }

        public override EndpointAddress BuildLocalAddress(string queueName)
        {
            throw new NotImplementedException();
        }

        public override string ToTransportAddress(EndpointAddress endpointAddress)
        {
            var queue = new StringBuilder(endpointAddress.Endpoint);

            if (endpointAddress.Discriminator != null)
            {
                queue.Append("-" + endpointAddress.Discriminator);
            }

            if (endpointAddress.Qualifier != null)
            {
                queue.Append("." + endpointAddress.Qualifier);
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

        public override bool SupportsTTBR { get; } = true;

        IRoutingTopology CreateRoutingTopology()
        {
            if (rabbitSettings.TopologyFactory == null)
            {
                throw new InvalidOperationException("A routing topology must be configured with one of the 'EndpointConfiguration.UseTransport<RabbitMQTransport>().UseXXXXRoutingTopology()` methods.");
            }

            return rabbitSettings.TopologyFactory(rabbitSettings.UseDurableExchangesAndQueues);
        }

        IPushMessages CreateMessagePump()
        {
            MessageConverter messageConverter;

            if (rabbitSettings.CustomMessageIdStrategy != null)
            {
                messageConverter = new MessageConverter(rabbitSettings.CustomMessageIdStrategy);
            }
            else
            {
                messageConverter = new MessageConverter();
            }

            var consumerTag = $"{settings.EndpointName.HostDisplayName}";

            var queuePurger = new QueuePurger(connectionFactory);

            return new MessagePump(connectionFactory, messageConverter, consumerTag, channelProvider, queuePurger, rabbitSettings.TimeToWaitBeforeTriggeringCircuitBreaker, rabbitSettings.PrefetchMultiplier, rabbitSettings.PrefetchCount, settings.CriticalErrorAction);
        }
    }
}
