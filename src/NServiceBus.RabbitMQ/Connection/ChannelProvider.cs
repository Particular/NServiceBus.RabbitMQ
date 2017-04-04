namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Concurrent;
    using global::RabbitMQ.Client;

    class ChannelProvider : IChannelProvider, IDisposable
    {
        public ChannelProvider(ConnectionFactory connectionFactory, IRoutingTopology routingTopology, bool usePublisherConfirms, bool allEndpointsSupportDelayedDelivery)
        {
            connection = new Lazy<IConnection>(() => connectionFactory.CreatePublishConnection());

            this.routingTopology = routingTopology;
            this.usePublisherConfirms = usePublisherConfirms;
            this.allEndpointsSupportDelayedDelivery = allEndpointsSupportDelayedDelivery;

            channels = new ConcurrentQueue<ConfirmsAwareChannel>();
        }

        public ConfirmsAwareChannel GetPublishChannel()
        {
            ConfirmsAwareChannel channel;

            if (!channels.TryDequeue(out channel) || channel.IsClosed)
            {
                channel?.Dispose();

                channel = new ConfirmsAwareChannel(connection.Value, routingTopology, usePublisherConfirms, allEndpointsSupportDelayedDelivery);
            }

            return channel;
        }

        public void ReturnPublishChannel(ConfirmsAwareChannel channel)
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
            //injected
        }

        void DisposeManaged()
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
        readonly bool allEndpointsSupportDelayedDelivery;
        readonly ConcurrentQueue<ConfirmsAwareChannel> channels;
    }
}
