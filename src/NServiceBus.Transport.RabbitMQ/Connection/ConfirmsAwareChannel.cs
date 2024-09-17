namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;

    sealed class ConfirmsAwareChannel(IConnection connection, IRoutingTopology routingTopology) : IDisposable
    {
        public bool IsOpen => channel.IsOpen;

        public bool IsClosed => channel.IsClosed;

        public async Task Initialize(CancellationToken cancellationToken = default)
        {
            channel = await connection.CreateChannelAsync(cancellationToken).ConfigureAwait(false);
            await channel.ConfirmSelectAsync(trackConfirmations: true, cancellationToken).ConfigureAwait(false);
        }

        public async Task SendMessage(string address, OutgoingMessage message, BasicProperties properties, CancellationToken cancellationToken = default)
        {
            if (properties.Headers != null && properties.Headers.TryGetValue(DelayInfrastructure.DelayHeader, out var delayValue))
            {
                var routingKey = DelayInfrastructure.CalculateRoutingKey((int)delayValue, address, out var startingDelayLevel);

                await routingTopology.BindToDelayInfrastructure(channel, address, DelayInfrastructure.DeliveryExchange, DelayInfrastructure.BindingKey(address), cancellationToken).ConfigureAwait(false);
                // TODO: Seems to be off that we use here the channel directly instead of the routingTopology
                await channel.BasicPublishAsync(DelayInfrastructure.LevelName(startingDelayLevel), routingKey, true, properties, message.Body, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await routingTopology.Send(channel, address, message, properties, cancellationToken).ConfigureAwait(false);
            }

            await channel.WaitForConfirmsOrDieAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task PublishMessage(Type type, OutgoingMessage message, BasicProperties properties, CancellationToken cancellationToken = default)
        {
            await routingTopology.Publish(channel, type, message, properties, cancellationToken).ConfigureAwait(false);
            await channel.WaitForConfirmsOrDieAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task RawSendInCaseOfFailure(string address, ReadOnlyMemory<byte> body, BasicProperties properties, CancellationToken cancellationToken = default)
        {
            await routingTopology.RawSendInCaseOfFailure(channel, address, body, properties, cancellationToken).ConfigureAwait(false);
            await channel.WaitForConfirmsOrDieAsync(cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            channel?.Dispose();
        }

        IChannel channel;
    }
}
