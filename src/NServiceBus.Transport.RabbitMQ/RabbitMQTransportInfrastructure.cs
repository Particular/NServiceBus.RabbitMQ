﻿namespace NServiceBus.Transport.RabbitMQ
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
        readonly BrokerVerifier brokerVerifier;
        readonly IRoutingTopology routingTopology;
        readonly TimeSpan networkRecoveryInterval;
        readonly bool supportsDelayedDelivery;

        public RabbitMQTransportInfrastructure(HostSettings hostSettings, ReceiveSettings[] receiverSettings,
            ConnectionFactory connectionFactory, IRoutingTopology routingTopology,
            ChannelProvider channelProvider, MessageConverter messageConverter,
            BrokerVerifier brokerVerifier,
            Action<IOutgoingTransportOperation, IBasicProperties> messageCustomization,
            TimeSpan timeToWaitBeforeTriggeringCircuitBreaker, PrefetchCountCalculation prefetchCountCalculation,
            TimeSpan networkRecoveryInterval, bool supportsDelayedDelivery)
        {
            this.connectionFactory = connectionFactory;
            this.routingTopology = routingTopology;
            this.channelProvider = channelProvider;
            this.brokerVerifier = brokerVerifier;
            this.networkRecoveryInterval = networkRecoveryInterval;
            this.supportsDelayedDelivery = supportsDelayedDelivery;

            Dispatcher = new MessageDispatcher(channelProvider, messageCustomization, supportsDelayedDelivery);
            Receivers = receiverSettings.Select(x => CreateMessagePump(hostSettings, x, messageConverter, timeToWaitBeforeTriggeringCircuitBreaker, prefetchCountCalculation))
                .ToDictionary(x => x.Id, x => x);
        }

        IMessageReceiver CreateMessagePump(HostSettings hostSettings, ReceiveSettings settings, MessageConverter messageConverter, TimeSpan timeToWaitBeforeTriggeringCircuitBreaker, PrefetchCountCalculation prefetchCountCalculation)
        {
            var consumerTag = $"{hostSettings.HostDisplayName} - {hostSettings.Name}";

            return new MessagePump(settings, connectionFactory, routingTopology, messageConverter, consumerTag, channelProvider, brokerVerifier, timeToWaitBeforeTriggeringCircuitBreaker, prefetchCountCalculation, hostSettings.CriticalErrorAction, networkRecoveryInterval);
        }

        internal async Task SetupInfrastructure(string[] sendingQueues, CancellationToken cancellationToken = default)
        {
            using var connection = await connectionFactory.CreateAdministrationConnection(cancellationToken).ConfigureAwait(false);

            using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

            if (supportsDelayedDelivery)
            {
                await DelayInfrastructure.Build(channel, cancellationToken).ConfigureAwait(false);
            }

            var receivingQueues = Receivers.Select(r => r.Value.ReceiveAddress).ToArray();

            await routingTopology.Initialize(channel, receivingQueues, sendingQueues, cancellationToken).ConfigureAwait(false);

            if (supportsDelayedDelivery)
            {
                foreach (string receivingAddress in receivingQueues)
                {
                    await routingTopology.BindToDelayInfrastructure(channel, receivingAddress,
                        DelayInfrastructure.DeliveryExchange, DelayInfrastructure.BindingKey(receivingAddress), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        public override async Task Shutdown(CancellationToken cancellationToken = default)
        {
            await Task.WhenAll(Receivers.Values.Select(r => r.StopReceive(cancellationToken)))
                .ConfigureAwait(false);

            await channelProvider.DisposeAsync().ConfigureAwait(false);
            brokerVerifier.Dispose();
        }

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
