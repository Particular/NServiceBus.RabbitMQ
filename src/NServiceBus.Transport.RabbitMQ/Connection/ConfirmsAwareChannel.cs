namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using NServiceBus.Logging;

    sealed class ConfirmsAwareChannel(IConnection connection, IRoutingTopology routingTopology) : IDisposable
    {
        public bool IsOpen => channel.IsOpen;

        public bool IsClosed => channel.IsClosed;

        public async Task Initialize(CancellationToken cancellationToken = default)
        {
            channel = await connection.CreateChannelAsync(new CreateChannelOptions
            {
                PublisherConfirmationsEnabled = true,
            }, cancellationToken: cancellationToken).ConfigureAwait(false);

            channel.BasicAcksAsync += Channel_BasicAcks;
            channel.BasicNacksAsync += Channel_BasicNacks;
            channel.BasicReturnAsync += Channel_BasicReturn;
            channel.ChannelShutdownAsync += Channel_ModelShutdown;
        }

        public async Task SendMessage(string address, OutgoingMessage message, BasicProperties properties, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource taskCompletionSource;

            try
            {
                await sequenceNumberSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                (taskCompletionSource, var registration) = await GetCancellableTaskCompletionSource(cancellationToken).ConfigureAwait(false);
                await using var _ = registration.ConfigureAwait(false);

                properties.SetConfirmationId(await channel.GetNextPublishSequenceNumberAsync(cancellationToken).ConfigureAwait(false));

                if (properties.Headers != null &&
                    properties.Headers.TryGetValue(DelayInfrastructure.DelayHeader, out var delayValue))
                {
                    var routingKey =
                        DelayInfrastructure.CalculateRoutingKey((int)delayValue, address, out var startingDelayLevel);

                    await routingTopology.BindToDelayInfrastructure(channel, address,
                        DelayInfrastructure.DeliveryExchange, DelayInfrastructure.BindingKey(address),
                        cancellationToken).ConfigureAwait(false);
                    // The channel is used here directly because it is not the routing topologies concern to know about the sends to the delay infrastructure
                    await channel.BasicPublishAsync(DelayInfrastructure.LevelName(startingDelayLevel), routingKey, true,
                        properties, message.Body, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await routingTopology.Send(channel, address, message, properties, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            finally
            {
                sequenceNumberSemaphore.Release();
            }

            await taskCompletionSource.Task.ConfigureAwait(false);
        }

        public async Task PublishMessage(Type type, OutgoingMessage message, BasicProperties properties, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource taskCompletionSource;

            try
            {
                await sequenceNumberSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                (taskCompletionSource, var registration) = await GetCancellableTaskCompletionSource(cancellationToken).ConfigureAwait(false);
                await using var _ = registration.ConfigureAwait(false);

                properties.SetConfirmationId(await channel.GetNextPublishSequenceNumberAsync(cancellationToken).ConfigureAwait(false));

                await routingTopology.Publish(channel, type, message, properties, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                sequenceNumberSemaphore.Release();
            }

            await taskCompletionSource.Task.ConfigureAwait(false);
        }

        public async Task RawSendInCaseOfFailure(string address, ReadOnlyMemory<byte> body, BasicProperties properties, CancellationToken cancellationToken = default)
        {
            properties.Headers ??= new Dictionary<string, object>();

            TaskCompletionSource taskCompletionSource;

            try
            {
                await sequenceNumberSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                (taskCompletionSource, var registration) = await GetCancellableTaskCompletionSource(cancellationToken).ConfigureAwait(false);

                await using var _ = registration.ConfigureAwait(false);

                properties.SetConfirmationId(await channel.GetNextPublishSequenceNumberAsync(cancellationToken).ConfigureAwait(false));

                await routingTopology.RawSendInCaseOfFailure(channel, address, body, properties, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                sequenceNumberSemaphore.Release();
            }

            await taskCompletionSource.Task.ConfigureAwait(false);
        }

        async ValueTask<(TaskCompletionSource, IAsyncDisposable)> GetCancellableTaskCompletionSource(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            // There is no need to capture the execution context therefore using UnsafeRegister
            var registration = cancellationToken.UnsafeRegister(static state =>
            {
                var (tcs, cancellationToken) = ((TaskCompletionSource, CancellationToken))state!;
                tcs.TrySetCanceled(cancellationToken);
            }, (tcs, cancellationToken));

            var nextPublishSequenceNumber = await channel.GetNextPublishSequenceNumberAsync(cancellationToken).ConfigureAwait(false);
            var added = messages.TryAdd(nextPublishSequenceNumber, tcs);

            if (!added)
            {
                throw new Exception($"Cannot publish a message with sequence number '{nextPublishSequenceNumber}' on this channel. A message was already published on this channel with the same confirmation number.");
            }

            return (tcs, registration);
        }

#pragma warning disable PS0018
        Task Channel_BasicAcks(object sender, BasicAckEventArgs e)
#pragma warning restore PS0018
        {
            if (!e.Multiple)
            {
                SetResult(e.DeliveryTag);
            }
            else
            {
                foreach (var message in messages)
                {
                    if (message.Key <= e.DeliveryTag)
                    {
                        SetResult(message.Key);
                    }
                }
            }

            return Task.CompletedTask;
        }

#pragma warning disable PS0018
        Task Channel_BasicNacks(object sender, BasicNackEventArgs e)
#pragma warning restore PS0018
        {
            if (!e.Multiple)
            {
                SetException(e.DeliveryTag, "Message rejected by broker.");
            }
            else
            {
                foreach (var message in messages)
                {
                    if (message.Key <= e.DeliveryTag)
                    {
                        SetException(message.Key, "Message rejected by broker.");
                    }
                }
            }

            return Task.CompletedTask;
        }

#pragma warning disable PS0018
        Task Channel_BasicReturn(object sender, BasicReturnEventArgs e)
#pragma warning restore PS0018
        {
            var message = $"Message could not be routed to {e.Exchange + e.RoutingKey}: {e.ReplyCode} {e.ReplyText}";

            if (e.BasicProperties.TryGetConfirmationId(out var deliveryTag))
            {
                SetException(deliveryTag, message);
            }
            else
            {
                Logger.Warn(message);
            }

            return Task.CompletedTask;
        }

#pragma warning disable PS0018
        Task Channel_ModelShutdown(object sender, ShutdownEventArgs e)
#pragma warning restore PS0018
        {
            do
            {
                foreach (var message in messages)
                {
                    SetException(message.Key, $"Channel has been closed: {e}");
                }
            }
            while (!messages.IsEmpty);

            return Task.CompletedTask;
        }

        void SetResult(ulong key)
        {
            if (messages.TryRemove(key, out var tcs))
            {
                tcs.SetResult();
            }
        }

        void SetException(ulong key, string exceptionMessage)
        {
            if (messages.TryRemove(key, out var tcs))
            {
                tcs.SetException(new Exception(exceptionMessage));
            }
        }

        public void Dispose() => channel?.Dispose();

        IChannel channel;
        readonly ConcurrentDictionary<ulong, TaskCompletionSource> messages = new();
        readonly SemaphoreSlim sequenceNumberSemaphore = new(1, 1);

        static readonly ILog Logger = LogManager.GetLogger(typeof(ConfirmsAwareChannel));
    }
}
