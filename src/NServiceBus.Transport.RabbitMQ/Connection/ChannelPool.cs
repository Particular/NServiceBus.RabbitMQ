namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Concurrent;
    using global::RabbitMQ.Client;

    sealed class ChannelPool : IDisposable
    {
        public ChannelPool(ConnectionFactory connectionFactory, IRoutingTopology routingTopology, bool usePublisherConfirms)
        {
            connection = new Lazy<IConnection>(() => connectionFactory.CreateMessagesConnection());

            this.routingTopology = routingTopology;
            this.usePublisherConfirms = usePublisherConfirms;

            channels = new ConcurrentQueue<ConfirmsAwareChannel>();
        }

        public ConfirmsAwareChannel GetChannel()
        {
            if (!channels.TryDequeue(out var channel) || channel.IsClosed)
            {
                channel?.Dispose();

                channel = new ConfirmsAwareChannel(connection.Value, routingTopology, usePublisherConfirms);
            }

            return channel;
        }

        public void ReturnChannel(ConfirmsAwareChannel channel)
        {
            if (channel.IsOpen)
            {
                channels.Enqueue(channel);
            }
            else
            {
                channel.Dispose();
            }
        }

        public void Dispose()
        {
            if (connection.IsValueCreated)
            {
                connection.Value.Dispose();
            }

            foreach (var channel in channels)
            {
                channel.Dispose();
            }
        }

        readonly Lazy<IConnection> connection;
        readonly IRoutingTopology routingTopology;
        readonly bool usePublisherConfirms;
        readonly ConcurrentQueue<ConfirmsAwareChannel> channels;
    }
}
