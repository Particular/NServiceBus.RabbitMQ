namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

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
            return new MessagePump(connectionFactory, routingTopology, messageConverter, consumerTag, channelProvider, timeToWaitBeforeTriggeringCircuitBreaker, prefetchCountCalculation, settings, hostSettings.CriticalErrorAction, networkRecoveryInterval);
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
    }
}
