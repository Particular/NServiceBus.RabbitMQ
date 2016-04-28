namespace NServiceBus.Transport.RabbitMQ
{
    using System.Collections.Concurrent;

    class ChannelProvider : IChannelProvider
    {
        public ChannelProvider(ConnectionManager connectionManager, bool usePublisherConfirms)
        {
            this.connectionManager = connectionManager;
            this.usePublisherConfirms = usePublisherConfirms;

            channels = new ConcurrentQueue<ConfirmsAwareChannel>();
        }

        public ConfirmsAwareChannel GetPublishChannel()
        {
            ConfirmsAwareChannel channel;

            if (!channels.TryDequeue(out channel) || channel.IsClosed)
            {
                channel?.Dispose();

                channel = new ConfirmsAwareChannel(connectionManager.GetPublishConnection(), usePublisherConfirms);
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

        readonly ConnectionManager connectionManager;
        readonly bool usePublisherConfirms;
        readonly ConcurrentQueue<ConfirmsAwareChannel> channels;
    }
}