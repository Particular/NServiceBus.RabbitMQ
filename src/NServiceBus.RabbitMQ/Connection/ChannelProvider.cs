namespace NServiceBus.Transport.RabbitMQ
{
    using global::RabbitMQ.Client;
    using System;
    using System.Collections.Concurrent;

    class ChannelProvider : IChannelProvider, IDisposable
    {
        public ChannelProvider(ConnectionFactory connectionFactory, bool usePublisherConfirms)
        {
            this.connectionFactory = connectionFactory;
            this.usePublisherConfirms = usePublisherConfirms;

            channels = new ConcurrentQueue<ConfirmsAwareChannel>();
        }

        public ConfirmsAwareChannel GetPublishChannel()
        {
            ConfirmsAwareChannel channel;

            if (!channels.TryDequeue(out channel) || channel.IsClosed)
            {
                channel?.Dispose();

                if (connection == null)
                {
                    CreateConnection();
                }

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

        void CreateConnection()
        {
            lock (connectionFactory)
            {
                connection = connection ?? connectionFactory.CreatePublishConnection();
            }
        }

        public void Dispose()
        {
            //injected
        }

        readonly ConnectionFactory connectionFactory;
        IConnection connection;
        readonly bool usePublisherConfirms;
        readonly ConcurrentQueue<ConfirmsAwareChannel> channels;
    }
}