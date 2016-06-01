namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using Logging;
    using Transports;

    class ConfirmsAwareChannel : IDisposable
    {
        public ConfirmsAwareChannel(IConnection connection, IRoutingTopology routingTopology, bool usePublisherConfirms)
        {
            channel = connection.CreateModel();

            this.routingTopology = routingTopology;
            this.usePublisherConfirms = usePublisherConfirms;

            if (usePublisherConfirms)
            {
                channel.ConfirmSelect();

                channel.BasicAcks += Channel_BasicAcks;
                channel.BasicNacks += Channel_BasicNacks;
                channel.BasicReturn += Channel_BasicReturn;
                channel.ModelShutdown += Channel_ModelShutdown;

                messages = new ConcurrentDictionary<ulong, TaskCompletionSource<bool>>();
            }
        }

        public IBasicProperties CreateBasicProperties() => channel.CreateBasicProperties();

        public bool IsOpen => channel.IsOpen;

        public bool IsClosed => channel.IsClosed;

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

            routingTopology.Send(channel, address, message, properties);

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
            var tcs = new TaskCompletionSource<bool>();
            var added = messages.TryAdd(channel.NextPublishSeqNo, tcs);

            if (!added) //debug check, this shouldn't happen
            {
                throw new Exception($"Failed to add {channel.NextPublishSeqNo}");
            }

            return tcs.Task;
        }

        void Channel_BasicAcks(object sender, BasicAckEventArgs e)
        {
            Task.Run(() =>
            {
                if (!e.Multiple)
                {
                    TaskCompletionSource<bool> tcs;
                    messages.TryRemove(e.DeliveryTag, out tcs);

                    tcs?.SetResult(true);
                }
                else
                {
                    foreach (var message in messages)
                    {
                        if (message.Key <= e.DeliveryTag)
                        {
                            TaskCompletionSource<bool> tcs;
                            messages.TryRemove(message.Key, out tcs);

                            tcs?.SetResult(true);
                        }
                    }
                }
            });
        }

        void Channel_BasicNacks(object sender, BasicNackEventArgs e)
        {
            Task.Run(() =>
            {
                if (!e.Multiple)
                {
                    TaskCompletionSource<bool> tcs;
                    messages.TryRemove(e.DeliveryTag, out tcs);

                    tcs?.SetException(new Exception("Message rejected by broker."));
                }
                else
                {
                    foreach (var message in messages)
                    {
                        if (message.Key <= e.DeliveryTag)
                        {
                            TaskCompletionSource<bool> tcs;
                            messages.TryRemove(message.Key, out tcs);

                            tcs?.SetException(new Exception("Message rejected by broker."));
                        }
                    }
                }
            });
        }

        void Channel_BasicReturn(object sender, BasicReturnEventArgs e)
        {
            Task.Run(() =>
            {
                var message = $"Message could not be routed to {e.Exchange + e.RoutingKey}: {e.ReplyCode} {e.ReplyText}";

                ulong deliveryTag;

                if (e.BasicProperties.TryGetConfirmationId(out deliveryTag))
                {
                    TaskCompletionSource<bool> tcs;
                    messages.TryRemove(deliveryTag, out tcs);

                    tcs?.SetException(new Exception(message));
                }
                else
                {
                    Logger.Warn(message);
                }
            });
        }

        void Channel_ModelShutdown(object sender, ShutdownEventArgs e)
        {
            Task.Run(() =>
            {
                do
                {
                    foreach (var message in messages)
                    {
                        TaskCompletionSource<bool> tcs;
                        messages.TryRemove(message.Key, out tcs);

                        tcs?.SetException(new Exception($"Channel has been closed: {e}"));
                    }
                } while (!messages.IsEmpty);
            });
        }

        public void Dispose()
        {
            //injected
        }

        IModel channel;
        readonly IRoutingTopology routingTopology;
        readonly bool usePublisherConfirms;
        readonly ConcurrentDictionary<ulong, TaskCompletionSource<bool>> messages;

        static readonly ILog Logger = LogManager.GetLogger(typeof(ConfirmsAwareChannel));
    }
}
