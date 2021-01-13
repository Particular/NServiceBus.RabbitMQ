namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using Logging;

    sealed class ConfirmsAwareChannel : IDisposable
    {
        public ConfirmsAwareChannel(IConnection connection, IRoutingTopology routingTopology)
        {
            channel = connection.CreateModel();
            channel.BasicAcks += Channel_BasicAcks;
            channel.BasicNacks += Channel_BasicNacks;
            channel.BasicReturn += Channel_BasicReturn;
            channel.ModelShutdown += Channel_ModelShutdown;

            channel.ConfirmSelect();

            this.routingTopology = routingTopology;

            messages = new ConcurrentDictionary<ulong, TaskCompletionSource<bool>>();
        }

        public IBasicProperties CreateBasicProperties() => channel.CreateBasicProperties();

        public bool IsOpen => channel.IsOpen;

        public bool IsClosed => channel.IsClosed;

        public Task SendMessage(string address, OutgoingMessage message, IBasicProperties properties)
        {
            var task = GetConfirmationTask();
            properties.SetConfirmationId(channel.NextPublishSeqNo);

            if (properties.Headers.TryGetValue(DelayInfrastructure.DelayHeader, out var delayValue))
            {
                var routingKey = DelayInfrastructure.CalculateRoutingKey((int)delayValue, address, out var startingDelayLevel);

                routingTopology.BindToDelayInfrastructure(channel, address, DelayInfrastructure.DeliveryExchange, DelayInfrastructure.BindingKey(address));
                channel.BasicPublish(DelayInfrastructure.LevelName(startingDelayLevel), routingKey, true, properties, message.Body);
            }
            else
            {
                routingTopology.Send(channel, address, message, properties);
            }

            return task;
        }

        public Task PublishMessage(Type type, OutgoingMessage message, IBasicProperties properties)
        {
            var task = GetConfirmationTask();
            properties.SetConfirmationId(channel.NextPublishSeqNo);

            routingTopology.Publish(channel, type, message, properties);

            return task;
        }

        public Task RawSendInCaseOfFailure(string address, ReadOnlyMemory<byte> body, IBasicProperties properties)
        {
            var task = GetConfirmationTask();

            if (properties.Headers == null)
            {
                properties.Headers = new Dictionary<string, object>();
            }

            properties.SetConfirmationId(channel.NextPublishSeqNo);

            routingTopology.RawSendInCaseOfFailure(channel, address, body, properties);

            return task;
        }

        Task GetConfirmationTask()
        {
            var tcs = new TaskCompletionSource<bool>();
            var added = messages.TryAdd(channel.NextPublishSeqNo, tcs);

            if (!added)
            {
                throw new Exception($"Cannot publish a message with sequence number '{channel.NextPublishSeqNo}' on this channel. A message was already published on this channel with the same confirmation number.");
            }

            return tcs.Task;
        }

        void Channel_BasicAcks(object sender, BasicAckEventArgs e)
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
        }

        void Channel_BasicNacks(object sender, BasicNackEventArgs e)
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
        }

        void Channel_BasicReturn(object sender, BasicReturnEventArgs e)
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
        }

        void Channel_ModelShutdown(object sender, ShutdownEventArgs e)
        {
            do
            {
                foreach (var message in messages)
                {
                    SetException(message.Key, $"Channel has been closed: {e}");
                }
            } while (!messages.IsEmpty);
        }

        void SetResult(ulong key)
        {
            if (messages.TryRemove(key, out var tcs))
            {
                _ = TaskEx.StartNew(tcs, state => ((TaskCompletionSource<bool>)state).SetResult(true));
            }
        }

        void SetException(ulong key, string exceptionMessage)
        {
            if (messages.TryRemove(key, out var tcs))
            {
                _ = TaskEx.StartNew(tcs, state => ((TaskCompletionSource<bool>)state).SetException(new Exception(exceptionMessage)));
            }
        }

        public void Dispose()
        {
            channel?.Dispose();
        }

        IModel channel;

        readonly IRoutingTopology routingTopology;
        readonly ConcurrentDictionary<ulong, TaskCompletionSource<bool>> messages;

        static readonly ILog Logger = LogManager.GetLogger(typeof(ConfirmsAwareChannel));
    }
}
