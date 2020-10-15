namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Text;
    using System.Threading.Tasks;

    sealed class RabbitMQTransportInfrastructure : TransportInfrastructure
    {
        readonly Settings settings;
        private readonly RabbitMQTransport rabbitSettings;
        readonly ConnectionFactory connectionFactory;
        readonly ChannelProvider channelProvider;
        readonly IRoutingTopology routingTopology;

        public RabbitMQTransportInfrastructure(Settings settings, RabbitMQTransport rabbitSettings)
        {
            this.settings = settings;
            this.rabbitSettings = rabbitSettings;

            var endpointName = this.settings.Name;
            var connectionConfiguration = ConnectionConfiguration.Create(rabbitSettings.ConnectionString, endpointName);

            connectionFactory = new ConnectionFactory(endpointName, connectionConfiguration, rabbitSettings.X509Certificate2Collection, rabbitSettings.DisableRemoteCertificateValidation, rabbitSettings.UseExternalAuthMechanism, rabbitSettings.HeartbeatInterval, rabbitSettings.NetworkRecoveryInterval);

            routingTopology = CreateRoutingTopology();

            channelProvider = new ChannelProvider(connectionFactory, connectionConfiguration.RetryDelay, routingTopology);
            channelProvider.CreateConnection();
            Dispatcher = new MessageDispatcher(channelProvider);

            using (var connection = connectionFactory.CreateAdministrationConnection())
            using (var channel = connection.CreateModel())
            {
                DelayInfrastructure.Build(channel);
                
                ////TODO: we need information about the infra queues we need to create upfront
                routingTopology.Initialize(channel, new[] { settings.Name+".audit", settings.Name+".error"}, new string[0]);
            }
        }

        public override TransportTransactionMode TransactionMode => TransportTransactionMode.ReceiveOnly;

        public override Task<IPushMessages> CreateReceiver(ReceiveSettings receiveSettings)
        {
            using (var connection = connectionFactory.CreateAdministrationConnection())
            using (var channel = connection.CreateModel())
            {
                routingTopology.Initialize(channel, new[] { receiveSettings.LocalAddress }, new string[0]);
                routingTopology.BindToDelayInfrastructure(channel, receiveSettings.LocalAddress, DelayInfrastructure.DeliveryExchange, DelayInfrastructure.BindingKey(receiveSettings.LocalAddress));
            }

            IManageSubscriptions subscriptionManager = null;
            if (receiveSettings.UsePublishSubscribe)
            {
                subscriptionManager = new SubscriptionManager(connectionFactory, routingTopology, receiveSettings.LocalAddress);
            }
            
            return Task.FromResult(CreateMessagePump(subscriptionManager, receiveSettings));
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

        ////TODO: not called anywhere atm.
        public Task Stop()
        {
            channelProvider.Dispose();
            return Task.CompletedTask;
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

        IPushMessages CreateMessagePump(IManageSubscriptions subscriptionManager, ReceiveSettings receiveSettings)
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

            var consumerTag = $"{settings.HostDisplayName}";

            var queuePurger = new QueuePurger(connectionFactory);

            return new MessagePump(connectionFactory, messageConverter, consumerTag, channelProvider, queuePurger, rabbitSettings.TimeToWaitBeforeTriggeringCircuitBreaker, rabbitSettings.PrefetchMultiplier, rabbitSettings.PrefetchCount, settings.CriticalErrorAction, subscriptionManager, receiveSettings.settings);
        }
    }
}
