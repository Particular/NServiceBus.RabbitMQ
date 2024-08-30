namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;

    sealed class ConfirmsAwareChannel : IDisposable
    {
        public ConfirmsAwareChannel(IConnection connection, IRoutingTopology routingTopology)
        {
            // TODO This should move into some for of initialization method
            channel = connection.CreateChannelAsync().GetAwaiter().GetResult();
            channel.ConfirmSelectAsync().GetAwaiter().GetResult();

            this.routingTopology = routingTopology;
        }

        public bool IsOpen => channel.IsOpen;

        public bool IsClosed => channel.IsClosed;

        public async Task SendMessage(string address, OutgoingMessage message, BasicProperties properties, CancellationToken cancellationToken = default)
        {
            if (properties.Headers.TryGetValue(DelayInfrastructure.DelayHeader, out var delayValue))
            {
                var routingKey = DelayInfrastructure.CalculateRoutingKey((int)delayValue, address, out var startingDelayLevel);

                await routingTopology.BindToDelayInfrastructure(channel, address, DelayInfrastructure.DeliveryExchange, DelayInfrastructure.BindingKey(address), cancellationToken).ConfigureAwait(false);
                await channel.BasicPublishAsync(DelayInfrastructure.LevelName(startingDelayLevel), routingKey, properties, message.Body, true, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await routingTopology.Send(channel, address, message, properties, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task PublishMessage(Type type, OutgoingMessage message, BasicProperties properties, CancellationToken cancellationToken = default)
        {
            await routingTopology.Publish(channel, type, message, properties, cancellationToken).ConfigureAwait(false);
        }

        public async Task RawSendInCaseOfFailure(string address, ReadOnlyMemory<byte> body, BasicProperties properties, CancellationToken cancellationToken = default)
        {
            properties.Headers ??= new Dictionary<string, object>();

            await routingTopology.RawSendInCaseOfFailure(channel, address, body, properties, cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            channel?.Dispose();
        }

        readonly IChannel channel;
        readonly IRoutingTopology routingTopology;
    }
}
