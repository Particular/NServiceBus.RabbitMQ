#nullable enable

namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using Logging;

    class ChannelProvider(ConnectionFactory connectionFactory, TimeSpan retryDelay, IRoutingTopology routingTopology)
        : IAsyncDisposable
    {
        public async Task Initialize(CancellationToken cancellationToken = default) => connection = await CreateConnectionWithShutdownListener(cancellationToken).ConfigureAwait(false);

        async Task<IConnection> CreateConnectionWithShutdownListener(CancellationToken cancellationToken)
        {
            var newConnection = await CreatePublishConnection(cancellationToken).ConfigureAwait(false);
            newConnection.ConnectionShutdownAsync += Connection_ConnectionShutdown;
            return newConnection;
        }

        protected virtual Task<IConnection> CreatePublishConnection(CancellationToken cancellationToken = default) => connectionFactory.CreatePublishConnection(cancellationToken);

#pragma warning disable PS0018
        Task Connection_ConnectionShutdown(object? sender, ShutdownEventArgs e)
#pragma warning restore PS0018
        {
            if (e.Initiator == ShutdownInitiator.Application || sender is null)
            {
                return Task.CompletedTask;
            }

            var connectionThatWasShutdown = (IConnection)sender;

            FireAndForget(cancellationToken => ReconnectSwallowingExceptions(connectionThatWasShutdown.ClientProvidedName, cancellationToken), stoppingTokenSource.Token);
            return Task.CompletedTask;
        }

        async Task ReconnectSwallowingExceptions(string? connectionName, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Logger.InfoFormat("'{0}': Attempting to reconnect in {1} seconds.", connectionName, retryDelay.TotalSeconds);

                try
                {
                    await DelayReconnect(cancellationToken).ConfigureAwait(false);

                    var newConnection = await CreateConnectionWithShutdownListener(cancellationToken).ConfigureAwait(false);

                    // A race condition is possible where CreatePublishConnection is invoked during Dispose
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

        public async ValueTask<ConfirmsAwareChannel> GetPublishChannel(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                var existing = Interlocked.CompareExchange(ref publishChannel, null, null);
                if (existing is not null && !existing.IsClosed)
                {
                    return existing;
                }

                var newChannel = new ConfirmsAwareChannel(connection!, routingTopology);
                await newChannel.Initialize(cancellationToken).ConfigureAwait(false);

                var expected = existing;
                var prev = Interlocked.CompareExchange(ref publishChannel, newChannel, expected);

                if (ReferenceEquals(prev, expected))
                {
                    if (expected is not null)
                    {
                        await expected.DisposeAsync().ConfigureAwait(false);
                    }

                    return newChannel;
                }

                await newChannel.DisposeAsync().ConfigureAwait(false);
            }
        }

        public ValueTask ReturnPublishChannel(ConfirmsAwareChannel channel, CancellationToken cancellationToken = default)
        {
            if (!channel.IsClosed)
            {
                return ValueTask.CompletedTask;
            }

            var prev = Interlocked.CompareExchange(ref publishChannel, null, channel);
            return ReferenceEquals(prev, channel) ? channel.DisposeAsync() : ValueTask.CompletedTask;
        }

#pragma warning disable PS0018
        public async ValueTask DisposeAsync()
#pragma warning restore PS0018
        {
            if (disposed)
            {
                return;
            }

            await stoppingTokenSource.CancelAsync().ConfigureAwait(false);
            stoppingTokenSource.Dispose();

            var oldConnection = Interlocked.Exchange(ref connection, null);
            oldConnection?.Dispose();

            var oldChannel = Interlocked.Exchange(ref publishChannel, null);
            if (oldChannel is not null)
            {
                await oldChannel.DisposeAsync().ConfigureAwait(false);
            }

            disposed = true;
        }

        readonly CancellationTokenSource stoppingTokenSource = new();
        volatile IConnection? connection;
        ConfirmsAwareChannel? publishChannel;
        bool disposed;

        static readonly ILog Logger = LogManager.GetLogger(typeof(ChannelProvider));
    }
}
