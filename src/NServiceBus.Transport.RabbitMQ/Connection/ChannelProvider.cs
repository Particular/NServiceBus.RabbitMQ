namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using Logging;

    sealed class ChannelProvider : IChannelProvider, IDisposable
    {
        public ChannelProvider(ConnectionFactory connectionFactory, TimeSpan retryDelay, IRoutingTopology routingTopology, bool usePublisherConfirms)
        {
            this.connectionFactory = connectionFactory;
            this.retryDelay = retryDelay;

            this.routingTopology = routingTopology;
            this.usePublisherConfirms = usePublisherConfirms;

            channels = new ConcurrentQueue<ConfirmsAwareChannel>();
        }

        public void CreateConnection()
        {
            connection = connectionFactory.CreatePublishConnection();
            connection.ConnectionShutdown += Connection_ConnectionShutdown;
        }

        void Connection_ConnectionShutdown(object sender, ShutdownEventArgs e)
        {
            if (e.Initiator != ShutdownInitiator.Application)
            {
                Logger.WarnFormat("Connection to the broker lost: {0}", e.ReplyText);

                Task.Run(Reconnect).Ignore();
            }
        }

        async Task Reconnect()
        {
            var reconnected = false;

            while (!reconnected)
            {
                Logger.InfoFormat("Attempting to reconnect in {0} seconds.", retryDelay.TotalSeconds);

                await Task.Delay(retryDelay).ConfigureAwait(false);

                try
                {
                    CreateConnection();
                    reconnected = true;

                    Logger.Info("Connection to the broker reestablished successfully.");
                }
                catch(Exception e)
                {
                    Logger.Info("Reconnecting to the broker failed.", e);
                }
            }
        }

        public ConfirmsAwareChannel GetPublishChannel()
        {
            if (!channels.TryDequeue(out var channel) || channel.IsClosed)
            {
                channel?.Dispose();

                channel = new ConfirmsAwareChannel(connection, routingTopology, usePublisherConfirms);
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
            if (connection != null)
            {
                connection.Dispose();
            }

            foreach (var channel in channels)
            {
                channel.Dispose();
            }
        }

        readonly ConnectionFactory connectionFactory;
        readonly TimeSpan retryDelay;
        readonly IRoutingTopology routingTopology;
        readonly bool usePublisherConfirms;
        readonly ConcurrentQueue<ConfirmsAwareChannel> channels;
        IConnection connection;

        static readonly ILog Logger = LogManager.GetLogger(typeof(ChannelProvider));
    }
}
