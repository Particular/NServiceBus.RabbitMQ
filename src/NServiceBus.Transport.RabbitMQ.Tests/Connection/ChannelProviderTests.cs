namespace NServiceBus.Transport.RabbitMQ.Tests.ConnectionString
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using Microsoft.Extensions.Time.Testing;
    using NUnit.Framework;

    [TestFixture]
    public class ChannelProviderTests
    {
        [Test]
        public async Task Should_recover_connection_and_dispose_old_one_when_connection_shutdown()
        {
            var channelProvider = new TestableChannelProvider();
            await channelProvider.Initialize();

            var publishConnection = channelProvider.PublishConnections.Dequeue();
            await publishConnection.RaiseConnectionShutdown(new ShutdownEventArgs(ShutdownInitiator.Library, 0, "Test"));

            var reconnectTask = channelProvider.FireAndForgetAction(CancellationToken.None);
            channelProvider.TimeProvider.Advance(TestableChannelProvider.RetryDelay);
            await reconnectTask;

            var recoveredConnection = channelProvider.PublishConnections.Dequeue();

            Assert.That(publishConnection.WasDisposed, Is.True);
            Assert.That(recoveredConnection.WasDisposed, Is.False);
        }

        [Test]
        public async Task Should_dispose_connection_when_disposed()
        {
            var channelProvider = new TestableChannelProvider();
            await channelProvider.Initialize();

            var publishConnection = channelProvider.PublishConnections.Dequeue();
            await channelProvider.DisposeAsync();

            Assert.That(publishConnection.WasDisposed, Is.True);
        }

        [Test]
        public async Task Should_not_attempt_to_recover_during_dispose_when_retry_delay_still_pending()
        {
            var channelProvider = new TestableChannelProvider();
            await channelProvider.Initialize();

            var publishConnection = channelProvider.PublishConnections.Dequeue();
            await publishConnection.RaiseConnectionShutdown(new ShutdownEventArgs(ShutdownInitiator.Library, 0, "Test"));

            // Deliberately not advancing time before disposing to simulate a pending retry delay
            await channelProvider.DisposeAsync();

            await channelProvider.FireAndForgetAction(CancellationToken.None);

            Assert.That(publishConnection.WasDisposed, Is.True);
            Assert.That(channelProvider.PublishConnections.TryDequeue(out _), Is.False);
        }

        [Test]
        public async Task Should_dispose_newly_established_connection()
        {
            var channelProvider = new TestableChannelProvider();
            await channelProvider.Initialize();

            var publishConnection = channelProvider.PublishConnections.Dequeue();
            await publishConnection.RaiseConnectionShutdown(new ShutdownEventArgs(ShutdownInitiator.Library, 0, "Test"));

            // Simulates disposing the channel provider while the reconnection loop is creating a new connection
            var releaseConnection = channelProvider.HoldConnectionCreation();
            var fireAndForgetTask = channelProvider.FireAndForgetAction(CancellationToken.None);
            channelProvider.TimeProvider.Advance(TestableChannelProvider.RetryDelay);
            await channelProvider.ConnectionCreationStarted;

            await channelProvider.DisposeAsync();

            releaseConnection.SetResult();
            await fireAndForgetTask;

            var recoveredConnection = channelProvider.PublishConnections.Dequeue();

            Assert.That(publishConnection.WasDisposed, Is.True);
            Assert.That(recoveredConnection.WasDisposed, Is.True);
        }

        class TestableChannelProvider(FakeTimeProvider timeProvider) : ChannelProvider(null!, RetryDelay, null!, timeProvider)
        {
            public static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

            public TestableChannelProvider() : this(new FakeTimeProvider())
            {
            }

            public FakeTimeProvider TimeProvider { get; } = timeProvider;

            public Queue<FakeConnection> PublishConnections { get; } = new();

            public Func<CancellationToken, Task> FireAndForgetAction { get; private set; } = _ => Task.CompletedTask;

            public Task ConnectionCreationStarted => connectionCreationStarted.Task;

            public TaskCompletionSource HoldConnectionCreation() => connectionCreationGate = new(TaskCreationOptions.RunContinuationsAsynchronously);

            protected override async Task<IConnection> CreatePublishConnection(CancellationToken cancellationToken = default)
            {
                var connection = new FakeConnection();
                PublishConnections.Enqueue(connection);

                if (connectionCreationGate is { } gate)
                {
                    connectionCreationStarted.TrySetResult();
                    await gate.Task;
                }

                return connection;
            }

            protected override void FireAndForget(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
                => FireAndForgetAction = _ => action(cancellationToken);

            readonly TaskCompletionSource connectionCreationStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource? connectionCreationGate;
        }

        class FakeConnection : IConnection
        {
            public int LocalPort { get; }

            public int RemotePort { get; }

            public void Dispose() => WasDisposed = true;

            public bool WasDisposed { get; private set; }

            public Task<IChannel> CreateChannelAsync(CreateChannelOptions? options = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            public ushort ChannelMax { get; }
            public IDictionary<string, object?> ClientProperties { get; } = new Dictionary<string, object?>();
            public ShutdownEventArgs? CloseReason { get; }
            public AmqpTcpEndpoint Endpoint { get; } = new AmqpTcpEndpoint();
            public uint FrameMax { get; }
            public TimeSpan Heartbeat { get; }
            public bool IsOpen { get; }
            public AmqpTcpEndpoint[]? KnownHosts { get; }
            public IProtocol Protocol { get; } = new FakeProtocol();
            public IDictionary<string, object?>? ServerProperties { get; }
            public IList<ShutdownReportEntry>? ShutdownReport { get; }
            public string ClientProvidedName { get; } = $"FakeConnection{Interlocked.Increment(ref connectionCounter)}";
            public event AsyncEventHandler<CallbackExceptionEventArgs> CallbackExceptionAsync = (_, _) => Task.CompletedTask;
            public event AsyncEventHandler<ShutdownEventArgs> ConnectionShutdownAsync = (_, _) => Task.CompletedTask;
            public event AsyncEventHandler<AsyncEventArgs> RecoverySucceededAsync = (_, _) => Task.CompletedTask;
            public event AsyncEventHandler<ConnectionRecoveryErrorEventArgs> ConnectionRecoveryErrorAsync = (_, _) => Task.CompletedTask;
            public event AsyncEventHandler<ConsumerTagChangedAfterRecoveryEventArgs> ConsumerTagChangeAfterRecoveryAsync = (_, _) => Task.CompletedTask;
            public event AsyncEventHandler<QueueNameChangedAfterRecoveryEventArgs> QueueNameChangedAfterRecoveryAsync = (_, _) => Task.CompletedTask;
            public event AsyncEventHandler<RecoveringConsumerEventArgs> RecoveringConsumerAsync = (_, _) => Task.CompletedTask;
            public event AsyncEventHandler<ConnectionBlockedEventArgs> ConnectionBlockedAsync = (_, _) => Task.CompletedTask;
            public event AsyncEventHandler<AsyncEventArgs> ConnectionUnblockedAsync = (_, _) => Task.CompletedTask;

            IEnumerable<ShutdownReportEntry> IConnection.ShutdownReport => throw new NotImplementedException();

            public Task RaiseConnectionShutdown(ShutdownEventArgs args) => ConnectionShutdownAsync.Invoke(this, args);
            public Task UpdateSecretAsync(string newSecret, string reason, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task CloseAsync(ushort reasonCode, string reasonText, TimeSpan timeout, bool abort, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            static int connectionCounter;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        class FakeProtocol : IProtocol
        {
            public string ApiName => throw new NotImplementedException();

            public int DefaultPort => throw new NotImplementedException();

            public int MajorVersion => throw new NotImplementedException();

            public int MinorVersion => throw new NotImplementedException();

            public int Revision => throw new NotImplementedException();
        }
    }
}