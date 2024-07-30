#nullable enable

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

        public async Task<IConnection> CreateConnection(CancellationToken cancellationToken = default) => connection = await CreateConnectionWithShutdownListener(cancellationToken).ConfigureAwait(false);

        protected virtual Task<IConnection> CreatePublishConnection(CancellationToken cancellationToken = default) => connectionFactory.CreatePublishConnection(cancellationToken);

        async Task<IConnection> CreateConnectionWithShutdownListener(CancellationToken cancellationToken)
        {
            var newConnection = await CreatePublishConnection(cancellationToken).ConfigureAwait(false);
            newConnection.ConnectionShutdown += Connection_ConnectionShutdown;
            return newConnection;
        }

        void Connection_ConnectionShutdown(object? sender, ShutdownEventArgs e)
        {
            if (e.Initiator == ShutdownInitiator.Application || sender is null)
            {
                return;
            }

            var connectionThatWasShutdown = (IConnection)sender;

            FireAndForget(cancellationToken => ReconnectSwallowingExceptions(connectionThatWasShutdown.ClientProvidedName, cancellationToken), stoppingTokenSource.Token);
        }

        async Task ReconnectSwallowingExceptions(string connectionName, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Logger.InfoFormat("'{0}': Attempting to reconnect in {1} seconds.", connectionName, retryDelay.TotalSeconds);

                try
                {
                    await DelayReconnect(cancellationToken).ConfigureAwait(false);

                    var newConnection = await CreateConnectionWithShutdownListener(cancellationToken).ConfigureAwait(false);

                    // A  race condition is possible where CreatePublishConnection is invoked during Dispose
                    // where the returned connection isn't disposed so invoking Dispose to be sure
                    if (cancellationToken.IsCancellationRequested)
                    {
                        newConnection.Dispose();
                        break;
                    }

                    var oldConnection = Interlocked.Exchange(ref connection, newConnection);
                    oldConnection?.Dispose();
                    break;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    Logger.InfoFormat("'{0}': Stopped trying to reconnecting to the broker due to shutdown", connectionName);
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
            if (disposed)
            {
                return;
            }

            stoppingTokenSource.Cancel();
            stoppingTokenSource.Dispose();

            var oldConnection = Interlocked.Exchange(ref connection, null);
            oldConnection?.Dispose();

            foreach (var channel in channels)
            {
                channel.Dispose();
            }

            disposed = true;
        }

        readonly ConnectionFactory connectionFactory;
        readonly TimeSpan retryDelay;
        readonly IRoutingTopology routingTopology;
        readonly ConcurrentQueue<ConfirmsAwareChannel> channels;
        readonly CancellationTokenSource stoppingTokenSource = new();
        volatile IConnection? connection;
        bool disposed;

        static readonly ILog Logger = LogManager.GetLogger(typeof(ChannelProvider));
    }
}
