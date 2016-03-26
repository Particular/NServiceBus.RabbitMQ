namespace NServiceBus.Transport.RabbitMQ
{
    using System.Collections.Concurrent;

    class ChannelProvider : IChannelProvider
    {
        public ChannelProvider(ConnectionManager connectionManager, bool usePublisherConfirms, TimeSpan maxWaitTimeForConfirms)
        {
            this.connectionManager = connectionManager;
            this.usePublisherConfirms = usePublisherConfirms;
            this.maxWaitTimeForConfirms = maxWaitTimeForConfirms;

            channels = new ConcurrentQueue<ConfirmsAwareChannel>();
        }

        public ConfirmsAwareChannel GetPublishChannel()
        {
            ConfirmsAwareChannel channel;

            if (!channels.TryDequeue(out channel) || channel.IsClosed)
            {
                channel?.Dispose();

                channel = new ConfirmsAwareChannel(connectionManager.GetPublishConnection(), usePublisherConfirms, maxWaitTimeForConfirms);
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
        readonly TimeSpan maxWaitTimeForConfirms;
        readonly ConcurrentQueue<ConfirmsAwareChannel> channels;
    }
}