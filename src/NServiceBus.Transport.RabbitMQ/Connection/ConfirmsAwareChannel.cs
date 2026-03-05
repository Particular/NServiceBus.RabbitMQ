namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;

    sealed class ConfirmsAwareChannel(IConnection? connection, IRoutingTopology routingTopology) : IAsyncDisposable
    {
        public bool IsOpen => channel?.IsOpen ?? false;

        public bool IsClosed => channel?.IsClosed ?? true;

        public async Task Initialize(CancellationToken cancellationToken = default)
        {
            if (connection is null)
            {
                throw new InvalidOperationException("Connection is null.");
            }

            var createChannelOptions = new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true, outstandingPublisherConfirmationsRateLimiter: null);
            channel = await connection.CreateChannelAsync(createChannelOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask SendMessage(string address, OutgoingMessage message, BasicProperties properties, CancellationToken cancellationToken = default)
        {
            if (channel is null)
            {
                throw new InvalidOperationException("Channel is not initialized.");
            }

            if (properties.Headers != null && properties.Headers.TryGetValue(DelayInfrastructure.DelayHeader, out var delayValue))
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

        public async ValueTask PublishMessage(Type type, OutgoingMessage message, BasicProperties properties, CancellationToken cancellationToken = default)
        {
            if (channel is null)
            {
                throw new InvalidOperationException("Channel is not initialized.");
            }

            await routingTopology.Publish(channel, type, message, properties, cancellationToken)
              .ConfigureAwait(false);
        }

        public async ValueTask RawSendInCaseOfFailure(string address, ReadOnlyMemory<byte> body, BasicProperties properties, CancellationToken cancellationToken = default)
        {
            if (channel is null)
            {
                throw new InvalidOperationException("Channel is not initialized.");
            }

            properties.Headers ??= new Dictionary<string, object?>();

            await routingTopology.RawSendInCaseOfFailure(channel, address, body, properties, cancellationToken)
                .ConfigureAwait(false);
        }

#pragma warning disable PS0018
        public ValueTask DisposeAsync() => channel is not null ? channel.DisposeAsync() : ValueTask.CompletedTask;
#pragma warning restore PS0018

        IChannel? channel;
    }
}
