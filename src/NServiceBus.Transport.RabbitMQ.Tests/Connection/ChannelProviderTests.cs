namespace NServiceBus.Transport.RabbitMQ.Tests.ConnectionString
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using NUnit.Framework;

    [TestFixture]
    public class ChannelProviderTests
    {
        [Test]
        public async Task Should_recover_connection_and_dispose_old_one_when_connection_shutdown()
        {
            var channelProvider = new TestableChannelProvider();
            await channelProvider.CreateConnection();

            var publishConnection = channelProvider.PublishConnections.Dequeue();
            publishConnection.RaiseConnectionShutdown(new ShutdownEventArgs(ShutdownInitiator.Library, 0, "Test"));

            channelProvider.DelayTaskCompletionSource.SetResult();

            await channelProvider.FireAndForgetAction(CancellationToken.None);

            var recoveredConnection = channelProvider.PublishConnections.Dequeue();

            Assert.That(publishConnection.WasDisposed, Is.True);
            Assert.That(recoveredConnection.WasDisposed, Is.False);
        }

        [Test]
        public async Task Should_dispose_connection_when_disposed()
        {
            var channelProvider = new TestableChannelProvider();
            await channelProvider.CreateConnection();

            var publishConnection = channelProvider.PublishConnections.Dequeue();
            channelProvider.Dispose();

            Assert.That(publishConnection.WasDisposed, Is.True);
        }

        [Test]
        public async Task Should_not_attempt_to_recover_during_dispose_when_retry_delay_still_pending()
        {
            var channelProvider = new TestableChannelProvider();
            await channelProvider.CreateConnection();

            var publishConnection = channelProvider.PublishConnections.Dequeue();
            publishConnection.RaiseConnectionShutdown(new ShutdownEventArgs(ShutdownInitiator.Library, 0, "Test"));

            // Deliberately not completing the delay task with channelProvider.DelayTaskCompletionSource.SetResult(); before disposing
            // to simulate a pending delay task
            channelProvider.Dispose();

            await channelProvider.FireAndForgetAction(CancellationToken.None);

            Assert.That(publishConnection.WasDisposed, Is.True);
            Assert.That(channelProvider.PublishConnections.TryDequeue(out _), Is.False);
        }

        [Test]
        public async Task Should_dispose_newly_established_connection()
        {
            var channelProvider = new TestableChannelProvider();
            await channelProvider.CreateConnection();

            var publishConnection = channelProvider.PublishConnections.Dequeue();
            publishConnection.RaiseConnectionShutdown(new ShutdownEventArgs(ShutdownInitiator.Library, 0, "Test"));

            // This simulates the race of the reconnection loop being fired off with the delay task completed during
            // the disposal of the channel provider. To achieve that it is necessary to kick off the reconnection loop
            // and await its completion after the channel provider has been disposed.
            var fireAndForgetTask = channelProvider.FireAndForgetAction(CancellationToken.None);
            channelProvider.DelayTaskCompletionSource.SetResult();
            channelProvider.Dispose();

            await fireAndForgetTask;

            var recoveredConnection = channelProvider.PublishConnections.Dequeue();

            Assert.That(publishConnection.WasDisposed, Is.True);
            Assert.That(recoveredConnection.WasDisposed, Is.True);
        }

        class TestableChannelProvider() : ChannelProvider(null!, TimeSpan.Zero, null!)
        {
            public Queue<FakeConnection> PublishConnections { get; } = new();

            public TaskCompletionSource DelayTaskCompletionSource { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Func<CancellationToken, Task> FireAndForgetAction { get; private set; }

            protected override Task<IConnection> CreatePublishConnection(CancellationToken cancellationToken = default)
            {
                var connection = new FakeConnection();
                PublishConnections.Enqueue(connection);

                return Task.FromResult((IConnection)connection);
            }

            protected override void FireAndForget(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
                => FireAndForgetAction = _ => action(cancellationToken);

            protected override async Task DelayReconnect(CancellationToken cancellationToken = default)
            {
                await using var _ = cancellationToken.Register(() => DelayTaskCompletionSource.TrySetCanceled(cancellationToken));
                await DelayTaskCompletionSource.Task;
            }
        }

        class FakeConnection : IConnection
        {
            public int LocalPort { get; }

            public int RemotePort { get; }

            public void Dispose() => WasDisposed = true;

            public bool WasDisposed { get; private set; }

            public void UpdateSecret(string newSecret, string reason) => throw new NotImplementedException();

            public void Abort() => throw new NotImplementedException();

            public void Abort(ushort reasonCode, string reasonText) => throw new NotImplementedException();

            public void Abort(TimeSpan timeout) => throw new NotImplementedException();

            public void Abort(ushort reasonCode, string reasonText, TimeSpan timeout) => throw new NotImplementedException();

            public void Close() => throw new NotImplementedException();

            public void Close(ushort reasonCode, string reasonText) => throw new NotImplementedException();

            public void Close(TimeSpan timeout) => throw new NotImplementedException();

            public void Close(ushort reasonCode, string reasonText, TimeSpan timeout) => throw new NotImplementedException();

            public void HandleConnectionBlocked(string reason) => throw new NotImplementedException();

            public void HandleConnectionUnblocked() => throw new NotImplementedException();

            public ushort ChannelMax { get; }
            public IDictionary<string, object> ClientProperties { get; }
            public ShutdownEventArgs CloseReason { get; }
            public AmqpTcpEndpoint Endpoint { get; }
            public uint FrameMax { get; }
            public TimeSpan Heartbeat { get; }
            public bool IsOpen { get; }
            public AmqpTcpEndpoint[] KnownHosts { get; }
            public IProtocol Protocol { get; }
            public IDictionary<string, object> ServerProperties { get; }
            public IList<ShutdownReportEntry> ShutdownReport { get; }
            public string ClientProvidedName { get; } = $"FakeConnection{Interlocked.Increment(ref connectionCounter)}";

            IEnumerable<ShutdownReportEntry> IConnection.ShutdownReport => throw new NotImplementedException();

            public event EventHandler<CallbackExceptionEventArgs> CallbackException = (_, _) => { };
            public event EventHandler<ConnectionBlockedEventArgs> ConnectionBlocked = (_, _) => { };
            public event EventHandler<ShutdownEventArgs> ConnectionShutdown = (_, _) => { };
            public event EventHandler<EventArgs> ConnectionUnblocked = (_, _) => { };
            public event EventHandler<EventArgs> RecoverySucceeded;
            public event EventHandler<ConnectionRecoveryErrorEventArgs> ConnectionRecoveryError;
            public event EventHandler<ConsumerTagChangedAfterRecoveryEventArgs> ConsumerTagChangeAfterRecovery;
            public event EventHandler<QueueNameChangedAfterRecoveryEventArgs> QueueNameChangedAfterRecovery;
            public event EventHandler<RecoveringConsumerEventArgs> RecoveringConsumer;

            public void RaiseConnectionShutdown(ShutdownEventArgs args) => ConnectionShutdown?.Invoke(this, args);
            public Task UpdateSecretAsync(string newSecret, string reason, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task CloseAsync(ushort reasonCode, string reasonText, TimeSpan timeout, bool abort, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

            static int connectionCounter;
        }
    }
}