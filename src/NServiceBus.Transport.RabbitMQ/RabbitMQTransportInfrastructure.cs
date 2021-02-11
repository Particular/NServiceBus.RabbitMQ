namespace NServiceBus.Transport.RabbitMQ
{
    using System.Linq;
    using System;
    using System.Threading.Tasks;

    sealed class RabbitMQTransportInfrastructure : TransportInfrastructure
    {
        readonly ConnectionFactory connectionFactory;
        readonly ChannelProvider channelProvider;
        IRoutingTopology routingTopology;

        public RabbitMQTransportInfrastructure(HostSettings hostSettings, ReceiveSettings[] receiverSettings, ConnectionFactory connectionFactory, IRoutingTopology routingTopology,
            ChannelProvider channelProvider, MessageConverter messageConverter,
            TimeSpan timeToWaitBeforeTriggeringCircuitBreaker, Func<int, int> prefetchCountCalculation)
        {
            this.connectionFactory = connectionFactory;
            this.routingTopology = routingTopology;
            this.channelProvider = channelProvider;

            Dispatcher = new MessageDispatcher(channelProvider);
            Receivers = receiverSettings.Select(x => CreateMessagePump(hostSettings, x, messageConverter, timeToWaitBeforeTriggeringCircuitBreaker, prefetchCountCalculation)).ToList().AsReadOnly();
        }

        IMessageReceiver CreateMessagePump(HostSettings hostSettings, ReceiveSettings settings, MessageConverter messageConverter,
            TimeSpan timeToWaitBeforeTriggeringCircuitBreaker, Func<int, int> prefetchCountCalculation)
        {
            var consumerTag = $"{hostSettings.HostDisplayName} - {hostSettings.Name}";
            return new MessagePump(connectionFactory, routingTopology, messageConverter, consumerTag, channelProvider, timeToWaitBeforeTriggeringCircuitBreaker, prefetchCountCalculation, settings, hostSettings.CriticalErrorAction);
        }

        public override Task DisposeAsync()
        {
            foreach (IMessageReceiver receiver in Receivers)
            {
                ((MessagePump)receiver).Dispose();
            }

            channelProvider.Dispose();
            return Task.CompletedTask;
        }
    }
}
