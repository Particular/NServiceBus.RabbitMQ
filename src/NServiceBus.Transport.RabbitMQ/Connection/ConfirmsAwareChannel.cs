namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;

    sealed class ConfirmsAwareChannel(IConnection connection, IRoutingTopology routingTopology) : IDisposable
    {
        public bool IsOpen => channel.IsOpen;

        public bool IsClosed => channel.IsClosed;

        public async Task Initialize(CancellationToken cancellationToken = default) =>
            channel = await connection.CreateChannelAsync(new CreateChannelOptions
            {
                PublisherConfirmationsEnabled = true,
                PublisherConfirmationTrackingEnabled = true
            }, cancellationToken: cancellationToken).ConfigureAwait(false);

        public async ValueTask SendMessage(string address, OutgoingMessage message, BasicProperties properties, CancellationToken cancellationToken = default)
        {
            if (properties.Headers != null &&
                properties.Headers.TryGetValue(DelayInfrastructure.DelayHeader, out var delayValue))
            {
                var routingKey =
                    DelayInfrastructure.CalculateRoutingKey((int)delayValue, address, out var startingDelayLevel);

                await routingTopology.BindToDelayInfrastructure(channel, address,
                    DelayInfrastructure.DeliveryExchange, DelayInfrastructure.BindingKey(address),
                    cancellationToken).ConfigureAwait(false);
                // The channel is used here directly because it is not the routing topologies concern to know about the sends to the delay infrastructure
                await channel.BasicPublishAsync(DelayInfrastructure.LevelName(startingDelayLevel), routingKey, true,
                    properties, message.Body, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await routingTopology.Send(channel, address, message, properties, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        public async ValueTask PublishMessage(Type type, OutgoingMessage message, BasicProperties properties, CancellationToken cancellationToken = default) =>
            await routingTopology.Publish(channel, type, message, properties, cancellationToken)
                .ConfigureAwait(false);

        public async ValueTask RawSendInCaseOfFailure(string address, ReadOnlyMemory<byte> body, BasicProperties properties, CancellationToken cancellationToken = default)
        {
            properties.Headers ??= new Dictionary<string, object>();

            await routingTopology.RawSendInCaseOfFailure(channel, address, body, properties, cancellationToken)
                .ConfigureAwait(false);
        }

        public void Dispose() => channel?.Dispose();

        IChannel channel;
    }
}
