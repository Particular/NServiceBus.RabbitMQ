namespace NServiceBus.Transport.RabbitMQ
{
    using global::RabbitMQ.Client;
    using System;
    using System.Collections.Concurrent;

    class ChannelProvider : IChannelProvider, IDisposable
    {
        public ChannelProvider(ConnectionFactory connectionFactory, bool usePublisherConfirms)
        {
            //this.connectionFactory = connectionFactory;
            connection = connectionFactory.CreatePublishConnection();
            this.usePublisherConfirms = usePublisherConfirms;

            channels = new ConcurrentQueue<ConfirmsAwareChannel>();
        }

        public ConfirmsAwareChannel GetPublishChannel()
        {
            ConfirmsAwareChannel channel;

            if (!channels.TryDequeue(out channel) || channel.IsClosed)
            {
                channel?.Dispose();

                channel = new ConfirmsAwareChannel(connection, usePublisherConfirms);
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

        //readonly ConnectionFactory connectionFactory;
        IConnection connection;
        readonly bool usePublisherConfirms;
        readonly ConcurrentQueue<ConfirmsAwareChannel> channels;
    }
}