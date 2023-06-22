namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using Logging;

    sealed class ChannelProvider : IAsyncDisposable
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
            if (connection is not null)
            {
                connection.ConnectionShutdown -= Connection_ConnectionShutdown;
                connection.Dispose();
                connection = null;
            }
            connection = connectionFactory.CreatePublishConnection(); // Can take over 5 seconds
            connection.ConnectionShutdown += Connection_ConnectionShutdown;
        }

        void Connection_ConnectionShutdown(object sender, ShutdownEventArgs e)
        {
            if (e.Initiator != ShutdownInitiator.Application)
            {
                var connection = (IConnection)sender;

                // Task.Run() to clarify intent that the call MUST return immediately and not rely on current async call stack behavior
                reconnectTask = Task.Run(() => ReconnectSwallowingExceptions(connection.ClientProvidedName, stoppingTokenSource.Token), CancellationToken.None);
            }
        }

        async Task ReconnectSwallowingExceptions(string connectionName, CancellationToken cancellationToken)
        {
            while (true)
            {
                Logger.InfoFormat("'{0}': Attempting to reconnect in {1} seconds.", connectionName, retryDelay.TotalSeconds);

                try
                {
                    await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                    CreateConnection();
                    break;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    Logger.InfoFormat("'{0}': Stopped trying to reconnecting to the broker due to shutdown", connectionName);
                    return;
                }
                catch (Exception ex)
                {
                    Logger.InfoFormat("'{0}': Reconnecting to the broker failed: {1}", connectionName, ex);
                }
            }

            Logger.InfoFormat("'{0}': Connection to the broker reestablished successfully.", connectionName);
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

#pragma warning disable PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
        public async ValueTask DisposeAsync()
#pragma warning restore PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
        {
            stoppingTokenSource.Cancel();
            stoppingTokenSource.Dispose();

            await reconnectTask.ConfigureAwait(false);

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
        readonly CancellationTokenSource stoppingTokenSource = new();
        Task reconnectTask = Task.CompletedTask;
        IConnection connection;

        static readonly ILog Logger = LogManager.GetLogger(typeof(ChannelProvider));
    }
}
