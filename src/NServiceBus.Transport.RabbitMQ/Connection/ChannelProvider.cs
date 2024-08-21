namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using Logging;

    class ChannelProvider : IDisposable
    {
        public ChannelProvider(ConnectionFactory connectionFactory, TimeSpan retryDelay, IRoutingTopology routingTopology)
        {
            this.connectionFactory = connectionFactory;
            this.retryDelay = retryDelay;

            this.routingTopology = routingTopology;

            channels = new ConcurrentQueue<ConfirmsAwareChannel>();
        }

        public void CreateConnection() => connection = CreateConnectionWithShutdownListener();

        protected virtual IConnection CreatePublishConnection() => connectionFactory.CreatePublishConnection();

        IConnection CreateConnectionWithShutdownListener()
        {
            var newConnection = CreatePublishConnection();
            newConnection.ConnectionShutdown += Connection_ConnectionShutdown;
            return newConnection;
        }


        void Connection_ConnectionShutdown(object sender, ShutdownEventArgs e)
        {
            if (e.Initiator != ShutdownInitiator.Application)
            {
                var connection = (IConnection)sender;

                FireAndForget(_ => ReconnectSwallowingExceptions(connection.ClientProvidedName));
            }
        }

#pragma warning disable PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
        async Task ReconnectSwallowingExceptions(string connectionName)
#pragma warning restore PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
        {
            while (true)
            {
                Logger.InfoFormat("'{0}': Attempting to reconnect in {1} seconds.", connectionName, retryDelay.TotalSeconds);

                await DelayReconnect().ConfigureAwait(false);

                try
                {
                    CreateConnection();
                    break;
                }
                catch (Exception ex)
                {
                    Logger.WarnFormat("'{0}': Reconnecting to the broker failed: {1}", connectionName, ex);
                }
            }

            Logger.InfoFormat("'{0}': Connection to the broker reestablished successfully.", connectionName);
        }

        protected virtual void FireAndForget(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default) =>
            // Task.Run() so the call returns immediately instead of waiting for the first await or return down the call stack
            _ = Task.Run(() => action(cancellationToken), CancellationToken.None);

        protected virtual Task DelayReconnect(CancellationToken cancellationToken = default) => Task.Delay(retryDelay, cancellationToken);

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
            connection?.Dispose();

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
