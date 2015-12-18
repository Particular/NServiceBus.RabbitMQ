namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using NServiceBus.Transports.RabbitMQ.Routing;
    using Roslyn.Utilities;

    class RabbitMqMessagePump : IPushMessages, IDisposable
    {
        public RabbitMqMessagePump(IManageRabbitMqConnections connectionManager, IRoutingTopology routingTopology, IChannelProvider channelProvider, ReceiveOptions receiveOptions)
        {
            this.connectionManager = connectionManager;
            this.receiveOptions = receiveOptions;
            this.routingTopology = routingTopology;
            this.channelProvider = channelProvider;
        }

        static RepeatedFailuresOverTimeCircuitBreaker SetupCircuitBreaker(CriticalError criticalError)
        {

            var timeToWaitBeforeTriggering = TimeSpan.FromMinutes(2);
            var timeToWaitBeforeTriggeringOverride = ConfigurationManager.AppSettings["NServiceBus/RabbitMqDequeueStrategy/TimeToWaitBeforeTriggering"];

            if (!string.IsNullOrEmpty(timeToWaitBeforeTriggeringOverride))
            {
                timeToWaitBeforeTriggering = TimeSpan.Parse(timeToWaitBeforeTriggeringOverride);
            }

            var delayAfterFailure = TimeSpan.FromSeconds(5);
            var delayAfterFailureOverride = ConfigurationManager.AppSettings["NServiceBus/RabbitMqDequeueStrategy/DelayAfterFailure"];

            if (!string.IsNullOrEmpty(delayAfterFailureOverride))
            {
                delayAfterFailure = TimeSpan.Parse(delayAfterFailureOverride);
            }

            return new RepeatedFailuresOverTimeCircuitBreaker("RabbitMqConnectivity",
                timeToWaitBeforeTriggering,
                ex => criticalError.Raise("Repeated failures when communicating with the broker", ex),
                delayAfterFailure);
        }

        public Task Init(Func<PushContext, Task> pipe, CriticalError criticalError, PushSettings settings)
        {
            this.pipe = pipe;
            this.settings = settings;

            circuitBreaker = SetupCircuitBreaker(criticalError);

            noAck = settings.RequiredTransactionMode == TransportTransactionMode.None;

            if (receiveOptions.PurgeOnStartup)
            {
                Purge();
            }

            return TaskEx.Completed;
        }

        public async Task Stop()
        {
            if (isStopping)
            {
                return;
            }

            isStopping = true;

            if (tokenSource == null)
            {
                return;
            }

            tokenSource.Cancel();

            var tasksToWait = new List<Task>(2)
            {
                mainConcurrencyLimiter.WaitAsync()
            };
            if (secondaryConcurrencyLimiter != null)
            {
                tasksToWait.Add(secondaryConcurrencyLimiter.WaitAsync());
            }

            await Task.WhenAll(tasksToWait).ConfigureAwait(false);
            await Task.WhenAll(mainMessagePumpTask, secondaryMessagePumpTask).ConfigureAwait(false);

            tokenSource = null;
        }

        public void Start(PushRuntimeSettings limitations)
        {
            isStopping = false;

            if (receiveOptions.DefaultPrefetchCount > 0)
            {
                actualPrefetchCount = receiveOptions.DefaultPrefetchCount;
            }
            else
            {
                actualPrefetchCount = Convert.ToUInt16(limitations.MaxConcurrency);

                Logger.InfoFormat("No prefetch count configured, defaulting to {0} (the configured concurrency level)", actualPrefetchCount);
            }

            var secondaryReceiveSettings = receiveOptions.GetSettings(settings.InputQueue);

            tokenSource = new CancellationTokenSource();

            mainConcurrencyLimiter = new SemaphoreSlim(limitations.MaxConcurrency, limitations.MaxConcurrency);

            mainMessagePumpTask = Task.Run(() => StartConsumers(settings.InputQueue, mainConcurrencyLimiter, tokenSource.Token, limitations.MaxConcurrency), CancellationToken.None);
            
            if (secondaryReceiveSettings.IsEnabled)
            {
                secondaryConcurrencyLimiter = new SemaphoreSlim(secondaryReceiveSettings.MaximumConcurrencyLevel, secondaryReceiveSettings.MaximumConcurrencyLevel);

                secondaryMessagePumpTask = Task.Run(() => StartConsumers(secondaryReceiveSettings.ReceiveQueue, secondaryConcurrencyLimiter, tokenSource.Token, secondaryReceiveSettings.MaximumConcurrencyLevel), CancellationToken.None);

                Logger.InfoFormat("Secondary receiver for queue '{0}' initiated with concurrency '{1}'", secondaryReceiveSettings.ReceiveQueue, secondaryReceiveSettings.MaximumConcurrencyLevel);
            }
            else
            {
                secondaryMessagePumpTask = Task.Delay(0);
            }
        }

        async Task StartConsumers(string inputQueue, SemaphoreSlim concurrencyLimiter, CancellationToken token, int maxConcurrency)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await ConsumeMessages(inputQueue, concurrencyLimiter, token, maxConcurrency).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // For graceful shutdown purposes
                }
                catch (Exception ex)
                {
                    Logger.Error("RabbitMq Message pump failed", ex);
                    await circuitBreaker.Failure(ex).ConfigureAwait(false);
                }
            }
        }

        public void Dispose()
        {
            // Injected
        }

        async Task ConsumeMessages(string inputQueue, SemaphoreSlim limiter, CancellationToken token, int maxConcurrency)
        {
            try
            {
                using (var connection = connectionManager.GetConsumeConnection())
                {
                    var modelsPool = new ObjectPool<IModel>(() => connection.CreateModel(), maxConcurrency);

                    while (!token.IsCancellationRequested)
                    {
                        await limiter.WaitAsync(token).ConfigureAwait(false);

                        var channel = modelsPool.Allocate();
                        channel.BasicQos(0, actualPrefetchCount, false);

                        var consumer = new AsyncBasicConsumer(channel, token);

                        channel.BasicConsume(inputQueue, noAck, receiveOptions.ConsumerTag, consumer);

                        circuitBreaker.Success();

                        BasicDeliverEventArgs message;

                        try
                        {
                            message = await consumer.Queue.ReceiveAsync(token);
                        }
                        catch (OperationCanceledException)
                        {
                            //Ignore cancellations
                            break;
                        }

                        Task.Run(async () =>
                        {
                            try
                            {
                                await ProcessMessage(message, channel);

                                if (!noAck)
                                {

                                    channel.BasicAck(message.DeliveryTag, false);
                                }
                            }
                            catch (Exception)
                            {
                                if (!noAck)
                                {
                                    channel.BasicReject(message.DeliveryTag, true);
                                }
                            }
                        }, token)
                            .ContinueWith(_ =>
                            {
                                modelsPool.Free(channel);
                                limiter.Release();
                            }, token)
                            .Ignore();
                    }
                }
            }
            catch (EndOfStreamException)
            {
                // If no items are present and the queue is in a closed
                // state, or if at any time while waiting the queue
                // transitions to a closed state (by a call to Close()), this
                // method will throw EndOfStreamException.
                throw;
            }
            catch (IOException)
            {
                //Unable to write data to the transport connection: An existing connection was forcibly closed by the remote host.
                //This exception is expected because we are shutting down!
                if (!isStopping)
                {
                    throw;
                }
            }
        }

        async Task ProcessMessage(BasicDeliverEventArgs message, IModel channel)
        {
            Dictionary<string, string> headers = null;
            string messageId = null;
            var pushMessage = false;
            try
            {
                messageId = receiveOptions.Converter.RetrieveMessageId(message);
                headers = receiveOptions.Converter.RetrieveHeaders(message);
                pushMessage = true;
            }
            catch (Exception ex)
            {
                var error = $"Poison message detected with deliveryTag '{message.DeliveryTag}'. Message will be moved to '{settings.ErrorQueue}'.";
                Logger.Error(error, ex);

                try
                {
                    using (var errorChannel = channelProvider.GetNewPublishChannel())
                    {
                        routingTopology.RawSendInCaseOfFailure(errorChannel.Channel, settings.ErrorQueue, message.Body, message.BasicProperties);
                    }
                }
                catch (Exception ex2)
                {
                    Logger.Error($"Poison message failed to be moved to '{settings.ErrorQueue}'.", ex2);
                    throw;
                }
            }

            if (pushMessage)
            {
                var context = new ContextBag();

                string explicitCallbackAddress;


                if (headers.TryGetValue(Callbacks.HeaderKey, out explicitCallbackAddress))
                {
                    context.Set(new CallbackAddress(explicitCallbackAddress));
                }

                var pushContext = new PushContext(messageId, headers, new MemoryStream(message.Body ?? new byte[0]), new TransportTransaction(), context);

                await pipe(pushContext).ConfigureAwait(false);
            }
        }

        void Purge()
        {
            using (var connection = connectionManager.GetAdministrationConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueuePurge(settings.InputQueue);
            }
        }

        static ILog Logger = LogManager.GetLogger(typeof(RabbitMqMessagePump));

        RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;
        IManageRabbitMqConnections connectionManager;
        Func<PushContext, Task> pipe;
        PushSettings settings;
        CancellationTokenSource tokenSource;
        SemaphoreSlim mainConcurrencyLimiter, secondaryConcurrencyLimiter;
        Task mainMessagePumpTask, secondaryMessagePumpTask;
        bool noAck;
        readonly ReceiveOptions receiveOptions;
        readonly IRoutingTopology routingTopology;
        readonly IChannelProvider channelProvider;
        ushort actualPrefetchCount;
        bool isStopping;

        class AsyncBasicConsumer : DefaultBasicConsumer
        {
            public AsyncBasicConsumer(IModel model, CancellationToken token) : base(model)
            {
                Queue = new BufferBlock<BasicDeliverEventArgs>(new DataflowBlockOptions
                {
                    CancellationToken = token
                });
            }

            public override void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, byte[] body)
            {
                Queue.Post(new BasicDeliverEventArgs
                {
                    ConsumerTag = consumerTag,
                    DeliveryTag = deliveryTag,
                    Redelivered = redelivered,
                    Exchange = exchange,
                    RoutingKey = routingKey,
                    BasicProperties = properties,
                    Body = body
                });
            }

            public override void OnCancel()
            {
                base.OnCancel();
                Queue.Complete();
            }

            public BufferBlock<BasicDeliverEventArgs> Queue { get; }
        }
    }
}