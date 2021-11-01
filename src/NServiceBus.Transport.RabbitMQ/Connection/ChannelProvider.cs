namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using Logging;

    sealed class ChannelProvider : IDisposable
    {
        public ChannelProvider(ConnectionFactory connectionFactory, TimeSpan retryDelay, IRoutingTopology routingTopology)
        {
            this.connectionFactory = connectionFactory;
            this.retryDelay = retryDelay;

            this.routingTopology = routingTopology;

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
                var connection = (IConnection)sender;

                _ = Task.Run(() => Reconnect(connection.ClientProvidedName));
            }
        }

        async Task Reconnect(string connectionName)
        {
            var reconnected = false;

            while (!reconnected)
            {
                Logger.InfoFormat("'{0}': Attempting to reconnect in {1} seconds.", connectionName, retryDelay.TotalSeconds);

                await Task.Delay(retryDelay).ConfigureAwait(false);

                try
                {
                    CreateConnection();
                    reconnected = true;

                    Logger.InfoFormat("'{0}': Connection to the broker reestablished successfully.", connectionName);
                }
                catch (Exception e)
                {
                    Logger.InfoFormat("'{0}': Reconnecting to the broker failed: {1}", connectionName, e);
                }
            }
        }

        public ConfirmsAwareChannel GetPublishChannel()
        {
            if (!channels.TryDequeue(out var channel) || channel.IsClosed)
            {
                channel?.Dispose();

                channel = new ConfirmsAwareChannel(connection, routingTopology);
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
        readonly ConcurrentQueue<ConfirmsAwareChannel> channels;
        IConnection connection;

        static readonly ILog Logger = LogManager.GetLogger(typeof(ChannelProvider));
    }
}
