namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;

    sealed class RabbitMQTransportInfrastructure : TransportInfrastructure
    {
        readonly ConnectionFactory connectionFactory;
        readonly ChannelProvider channelProvider;
        readonly IRoutingTopology routingTopology;
        readonly TimeSpan networkRecoveryInterval;

        public RabbitMQTransportInfrastructure(HostSettings hostSettings, ReceiveSettings[] receiverSettings, ConnectionFactory connectionFactory, IRoutingTopology routingTopology,
            ChannelProvider channelProvider, MessageConverter messageConverter,
            TimeSpan timeToWaitBeforeTriggeringCircuitBreaker, PrefetchCountCalculation prefetchCountCalculation, TimeSpan networkRecoveryInterval)
        {
            this.connectionFactory = connectionFactory;
            this.routingTopology = routingTopology;
            this.channelProvider = channelProvider;
            this.networkRecoveryInterval = networkRecoveryInterval;

            Dispatcher = new MessageDispatcher(channelProvider);
            Receivers = receiverSettings.Select(x => CreateMessagePump(hostSettings, x, messageConverter, timeToWaitBeforeTriggeringCircuitBreaker, prefetchCountCalculation))
                .ToDictionary(x => x.Id, x => x);
        }

        IMessageReceiver CreateMessagePump(HostSettings hostSettings, ReceiveSettings settings, MessageConverter messageConverter,
            TimeSpan timeToWaitBeforeTriggeringCircuitBreaker, PrefetchCountCalculation prefetchCountCalculation)
        {
            var consumerTag = $"{hostSettings.HostDisplayName} - {hostSettings.Name}";
            var receiveAddress = ToTransportAddress(settings.ReceiveAddress);
            return new MessagePump(settings, connectionFactory, routingTopology, messageConverter, consumerTag, channelProvider, timeToWaitBeforeTriggeringCircuitBreaker, prefetchCountCalculation, hostSettings.CriticalErrorAction, networkRecoveryInterval);
        }

        internal void SetupInfrastructure(QueueMode queueMode, string[] sendingQueues, bool allowInputQueueConfigurationMismatch)
        {
            using (IConnection connection = connectionFactory.CreateAdministrationConnection())
            using (IModel channel = connection.CreateModel())
            {
                DelayInfrastructure.Build(channel);

                var receivingQueues = Receivers.Select(r => r.Value.ReceiveAddress).ToArray();

                routingTopology.Initialize(connection, receivingQueues, sendingQueues, queueMode != QueueMode.Classic, allowInputQueueConfigurationMismatch);

                foreach (string receivingAddress in receivingQueues)
                {
                    routingTopology.BindToDelayInfrastructure(channel, receivingAddress, DelayInfrastructure.DeliveryExchange, DelayInfrastructure.BindingKey(receivingAddress));
                }
            }
        }

        public override Task Shutdown(CancellationToken cancellationToken = default)
        {
            foreach (IMessageReceiver receiver in Receivers.Values)
            {
                ((MessagePump)receiver).Dispose();
            }

            channelProvider.Dispose();
            return Task.CompletedTask;
        }

        /// <summary>
        ///     Translates a <see cref="T:NServiceBus.Transport.QueueAddress" /> object into a transport specific queue
        ///     address-string.
        /// </summary>
        public override string ToTransportAddress(QueueAddress address) => TranslateAddress(address);

        internal static string TranslateAddress(QueueAddress address)
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
    }
}
