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
        public RabbitMqMessagePump(IManageRabbitMqConnections connectionManager, IRoutingTopology routingTopology, IChannelProvider channelProvider, ReceiveOptions receiveOptions, Callbacks callbacks)
        {
            this.connectionManager = connectionManager;
            this.receiveOptions = receiveOptions;
            this.callbacks = callbacks;
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

        public Task Init(Func<PushContext, Task> pipeline, CriticalError criticalError, PushSettings pushSettings)
        {
            pipe = pipeline;
            settings = pushSettings;

            circuitBreaker = SetupCircuitBreaker(criticalError);

            noAck = pushSettings.RequiredTransactionMode == TransportTransactionMode.None;

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

            if (parentSource == null)
            {
                return;
            }

            childrenSource.Cancel();

            var tasksToWait = WaitForSemaphores();

            await Task.WhenAll(tasksToWait).ConfigureAwait(false);

            parentSource.Cancel();

            await Task.WhenAll(mainMessagePumpTask, secondaryMessagePumpTask).ConfigureAwait(false);

            parentSource = null;
        }

        IEnumerable<Task> WaitForSemaphores()
        {
            for (var i = 0; i < mainConcurrency; i++)
            {
                yield return mainConcurrencyLimiter.WaitAsync();
            }

            if (secondaryConcurrencyLimiter != null)
            {
                for (var i = 0; i < secondaryConcurrency; i++)
                {
                    yield return secondaryConcurrencyLimiter.WaitAsync();
                }
            }
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

            parentSource = new CancellationTokenSource();
            childrenSource = new CancellationTokenSource();

            mainConcurrencyLimiter = new SemaphoreSlim(limitations.MaxConcurrency, limitations.MaxConcurrency);
            mainConcurrency = limitations.MaxConcurrency;

            mainMessagePumpTask = Task.Run(() => StartConsumers(settings.InputQueue, mainConcurrencyLimiter, parentSource.Token, limitations.MaxConcurrency), CancellationToken.None);
            
            if (secondaryReceiveSettings.IsEnabled)
            {
                secondaryConcurrencyLimiter = new SemaphoreSlim(secondaryReceiveSettings.MaximumConcurrencyLevel, secondaryReceiveSettings.MaximumConcurrencyLevel);
                secondaryConcurrency = secondaryReceiveSettings.MaximumConcurrencyLevel;
                secondaryMessagePumpTask = Task.Run(() => StartConsumers(secondaryReceiveSettings.ReceiveQueue, secondaryConcurrencyLimiter, parentSource.Token, secondaryReceiveSettings.MaximumConcurrencyLevel), CancellationToken.None);

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
                    await ConsumeMessages(inputQueue, concurrencyLimiter, childrenSource.Token, maxConcurrency).ConfigureAwait(false);
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
                var connection = connectionManager.GetConsumeConnection();
                var consumersPool = new ObjectPool<AsyncBasicConsumer>(() =>
                {
                    var channel = connection.CreateModel();
                    channel.BasicQos(0, actualPrefetchCount, false);

                    var consumer = new AsyncBasicConsumer(channel, token);
                    channel.BasicConsume(inputQueue, noAck, receiveOptions.ConsumerTag, consumer);

                    return consumer;
                }, maxConcurrency);

                circuitBreaker.Success();

                while (!token.IsCancellationRequested)
                {
                    await limiter.WaitAsync(token).ConfigureAwait(false);
                    try
                    {
                        var consumer = consumersPool.Allocate();
                        var message = await consumer.Queue.ReceiveAsync(token);

                        Task.Factory.StartNew(async obj =>
                        {
                            var consumerParam = (AsyncBasicConsumer) obj;
                            try
                            {
                                await ProcessMessage(message);

                                if (!noAck)
                                {
                                    consumerParam.Model.BasicAck(message.DeliveryTag, false);
                                }
                            }
                            catch (Exception)
                            {
                                if (!noAck)
                                {
                                    consumerParam.Model.BasicReject(message.DeliveryTag, true);
                                }
                            }
                            finally
                            {
                                consumersPool.Free(consumerParam);
                                limiter.Release();
                            }
                        }, consumer, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default)
                            .Ignore();
                    }
                    catch (OperationCanceledException)
                    {
                        limiter.Release();
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

        async Task ProcessMessage(BasicDeliverEventArgs message)
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
                context.Set(callbacks);

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
        CancellationTokenSource parentSource, childrenSource;
        SemaphoreSlim mainConcurrencyLimiter, secondaryConcurrencyLimiter;
        Task mainMessagePumpTask, secondaryMessagePumpTask;
        bool noAck;
        readonly ReceiveOptions receiveOptions;
        readonly Callbacks callbacks;
        readonly IRoutingTopology routingTopology;
        readonly IChannelProvider channelProvider;
        ushort actualPrefetchCount;
        bool isStopping;
        int mainConcurrency, secondaryConcurrency;

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