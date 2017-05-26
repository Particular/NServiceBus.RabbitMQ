namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using Logging;

    class ConfirmsAwareChannel : IDisposable
    {
        public ConfirmsAwareChannel(IConnection connection, IRoutingTopology routingTopology, bool usePublisherConfirms)
        {
            channel = connection.CreateModel();
            channel.BasicReturn += Channel_BasicReturn;

            this.routingTopology = routingTopology;

            delayTopology = routingTopology as ISupportDelayedDelivery;

            this.usePublisherConfirms = usePublisherConfirms;

            if (usePublisherConfirms)
            {
                channel.ConfirmSelect();

                channel.BasicAcks += Channel_BasicAcks;
                channel.BasicNacks += Channel_BasicNacks;
                channel.ModelShutdown += Channel_ModelShutdown;

                messages = new ConcurrentDictionary<ulong, TaskCompletionSource<bool>>();
            }
        }

        public IBasicProperties CreateBasicProperties() => channel.CreateBasicProperties();

        public bool IsOpen => channel.IsOpen;

        public bool IsClosed => channel.IsClosed;

        public bool SupportsDelayedDelivery => delayTopology != null;

        public Task SendMessage(string address, OutgoingMessage message, IBasicProperties properties)
        {
            Task task;

            if (usePublisherConfirms)
            {
                task = GetConfirmationTask();
                properties.SetConfirmationId(channel.NextPublishSeqNo);
            }
            else
            {
                task = TaskEx.CompletedTask;
            }

            if (properties.Headers.TryGetValue(DelayInfrastructure.DelayHeader, out var delayValue) && SupportsDelayedDelivery)
            {
                var routingKey = DelayInfrastructure.CalculateRoutingKey((int)delayValue, address, out var startingDelayLevel);

                delayTopology.BindToDelayInfrastructure(channel, address, DelayInfrastructure.DeliveryExchange, DelayInfrastructure.BindingKey(address));
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
            Task task;

            if (usePublisherConfirms)
            {
                task = GetConfirmationTask();
                properties.SetConfirmationId(channel.NextPublishSeqNo);
            }
            else
            {
                task = TaskEx.CompletedTask;
            }

            routingTopology.Publish(channel, type, message, properties);

            return task;
        }

        public Task RawSendInCaseOfFailure(string address, byte[] body, IBasicProperties properties)
        {
            Task task;

            if (usePublisherConfirms)
            {
                task = GetConfirmationTask();

                if (properties.Headers == null)
                {
                    properties.Headers = new Dictionary<string, object>();
                }

                properties.SetConfirmationId(channel.NextPublishSeqNo);
            }
            else
            {
                task = TaskEx.CompletedTask;
            }

            routingTopology.RawSendInCaseOfFailure(channel, address, body, properties);

            return task;
        }

        Task GetConfirmationTask()
        {
#if NET452
            var tcs = new TaskCompletionSource<bool>();
#else
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
#endif
            var added = messages.TryAdd(channel.NextPublishSeqNo, tcs);

            if (!added) //debug check, this shouldn't happen
            {
                throw new Exception($"Failed to add {channel.NextPublishSeqNo}");
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
#if NET452
                TaskEx.StartNew(tcs, state => ((TaskCompletionSource<bool>)state).SetResult(true)).Ignore();
#else
                tcs.SetResult(true);
#endif
            }
        }

        void SetException(ulong key, string exceptionMessage)
        {
            if (messages.TryRemove(key, out var tcs))
            {
#if NET452
                TaskEx.StartNew(tcs, state => ((TaskCompletionSource<bool>)state).SetException(new Exception(exceptionMessage))).Ignore();
#else
                tcs.SetException(new Exception(exceptionMessage));
#endif
            }
        }


        public void Dispose()
        {
            //injected
        }

        IModel channel;
        readonly IRoutingTopology routingTopology;
        readonly ISupportDelayedDelivery delayTopology;
        readonly bool usePublisherConfirms;
        readonly ConcurrentDictionary<ulong, TaskCompletionSource<bool>> messages;

        static readonly ILog Logger = LogManager.GetLogger(typeof(ConfirmsAwareChannel));
    }
}
