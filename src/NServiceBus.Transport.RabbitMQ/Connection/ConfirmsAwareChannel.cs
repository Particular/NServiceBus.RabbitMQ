namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;

    sealed class ConfirmsAwareChannel : IAsyncDisposable
    {
        ConfirmsAwareChannel(IChannel channel, IRoutingTopology routingTopology)
        {
            this.channel = channel;
            this.routingTopology = routingTopology;
        }

        public static async Task<ConfirmsAwareChannel> Create(IConnection? connection, IRoutingTopology routingTopology, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(connection);

            var createChannelOptions = new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true, outstandingPublisherConfirmationsRateLimiter: null);
            var channel = await connection.CreateChannelAsync(createChannelOptions, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var confirmsAwareChannel = new ConfirmsAwareChannel(channel, routingTopology);

            return confirmsAwareChannel;
        }

        public bool IsOpen => channel.IsOpen;

        public bool IsClosed => channel.IsClosed;

        public async ValueTask SendMessage(string address, OutgoingMessage message, BasicProperties properties, CancellationToken cancellationToken = default)
        {
            if (properties.Headers?.TryGetValue(DelayInfrastructure.DelayHeader, out var delayValue) ?? false)
            {
                var delay = Convert.ToInt32(delayValue);
                var routingKey = DelayInfrastructure.CalculateRoutingKey(delay, address, out var startingDelayLevel);

                await routingTopology.BindToDelayInfrastructure(channel, address, DelayInfrastructure.DeliveryExchange, DelayInfrastructure.BindingKey(address), cancellationToken)
                    .ConfigureAwait(false);

                // The channel is used here directly because it is not the routing topologies concern to know about the sends to the delay infrastructure
                await channel.BasicPublishAsync(DelayInfrastructure.LevelName(startingDelayLevel), routingKey, true, properties, message.Body, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await routingTopology.Send(channel, address, message, properties, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        public ValueTask PublishMessage(Type type, OutgoingMessage message, BasicProperties properties, CancellationToken cancellationToken = default) =>
            routingTopology.Publish(channel, type, message, properties, cancellationToken);

        public ValueTask RawSendInCaseOfFailure(string address, ReadOnlyMemory<byte> body, BasicProperties properties, CancellationToken cancellationToken = default)
        {
            properties.Headers ??= new Dictionary<string, object?>();

            return routingTopology.RawSendInCaseOfFailure(channel, address, body, properties, cancellationToken);
        }

        public ValueTask DisposeAsync() => channel.DisposeAsync();

        readonly IChannel channel;
        readonly IRoutingTopology routingTopology;
    }
}
