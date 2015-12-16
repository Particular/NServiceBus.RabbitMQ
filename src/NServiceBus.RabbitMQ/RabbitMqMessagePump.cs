namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using NServiceBus.Transports.RabbitMQ.Routing;

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

            if (settings.PurgeOnStartup)
            {
                Purge();
            }

            return TaskEx.Completed;
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

            actualConcurrencyLevel = limitations.MaxConcurrency + secondaryReceiveSettings.MaximumConcurrencyLevel;

            tokenSource = new CancellationTokenSource();

            // We need to add an extra one because if we fail and the count is at zero already, it doesn't allow us to add one more.
            tracksRunningThreads = new SemaphoreSlim(actualConcurrencyLevel, actualConcurrencyLevel);

            for (var i = 0; i < limitations.MaxConcurrency; i++)
            {
                StartConsumer(settings.InputQueue);
            }

            if (secondaryReceiveSettings.IsEnabled)
            {
                for (var i = 0; i < secondaryReceiveSettings.MaximumConcurrencyLevel; i++)
                {
                    StartConsumer(secondaryReceiveSettings.ReceiveQueue);
                }

                Logger.InfoFormat("Secondary receiver for queue '{0}' initiated with concurrency '{1}'", secondaryReceiveSettings.ReceiveQueue, secondaryReceiveSettings.MaximumConcurrencyLevel);
            }
        }

        public Task Stop()
        {
            if (isStopping)
            {
                return TaskEx.Completed;
            }

            isStopping = true;

            if (tokenSource == null)
            {
                return TaskEx.Completed;
            }

            tokenSource.Cancel();

            WaitForThreadsToStop();

            tokenSource = null;

            return TaskEx.Completed;
        }

        void WaitForThreadsToStop()
        {
            for (var index = 0; index < actualConcurrencyLevel; index++)
            {
                tracksRunningThreads.Wait();
            }

            tracksRunningThreads.Release(actualConcurrencyLevel);

            tracksRunningThreads.Dispose();
        }

        public void Dispose()
        {
            // Injected
        }

        void StartConsumer(string queue)
        {
            var token = tokenSource.Token;
            Task.Factory
                .StartNew(ConsumeMessages, new ConsumeParams
                {
                    Queue = queue,
                    CancellationToken = token
                }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default)
                .ContinueWith(t =>
                {
                    t.Exception.Handle(ex =>
                    {
                        Logger.Error("Failed to receive messages from " + queue, t.Exception);
                        circuitBreaker.Failure(ex);
                        return true;
                    });

                    if (!tokenSource.IsCancellationRequested)
                    {
                        StartConsumer(queue);
                    }
                }, TaskContinuationOptions.OnlyOnFaulted);
        }

        async Task ConsumeMessages(object state)
        {
            if (!tracksRunningThreads.Wait(TimeSpan.FromSeconds(1)))
            {
                return;
            }

            try
            {
                var parameters = (ConsumeParams) state;
                var connection = connectionManager.GetConsumeConnection();

                using (var channel = connection.CreateModel())
                {
                    channel.BasicQos(0, actualPrefetchCount, false);

                    var consumer = new QueueingBasicConsumer(channel);

                    channel.BasicConsume(parameters.Queue, noAck, receiveOptions.ConsumerTag, consumer);

                    circuitBreaker.Success();

                    while (!parameters.CancellationToken.IsCancellationRequested)
                    {
                        var message = DequeueMessage(consumer, receiveOptions.DequeueTimeout);

                        if (message == null)
                        {
                            continue;
                        }

                        try
                        {
                            var messageProcessedOk = false;
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

                                    //todo: trigger the circuit breaker
                                }

                                //just ack the poison message to avoid getting stuck
                                messageProcessedOk = true;
                            }

                            if (pushMessage)
                            {
                                var pushContext = new PushContext(messageId, headers, new MemoryStream(message.Body ?? new byte[0]), new TransportTransaction(), new ContextBag());

                                await pipe(pushContext).ConfigureAwait(false);
                            }

                            if (!noAck)
                            {
                                if (messageProcessedOk)
                                {
                                    channel.BasicAck(message.DeliveryTag, false);
                                }
                                else
                                {
                                    channel.BasicReject(message.DeliveryTag, true);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("A failure occurred at the transport while trying to dequeue/process a message.", ex);

                            if (!noAck)
                            {
                                channel.BasicReject(message.DeliveryTag, true);
                            }
                        }
                    }
                }
            }
            catch (EndOfStreamException)
            {
                // If no items are present and the queue is in a closed
                // state, or if at any time while waiting the queue
                // transitions to a closed state (by a call to Close()), this
                // method will throw EndOfStreamException.

                // We need to put a delay here otherwise we end-up doing a tight loop that causes
                // CPU spikes
                Thread.Sleep(1000);
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
            finally
            {
                tracksRunningThreads.Release();
            }
        }

        static BasicDeliverEventArgs DequeueMessage(QueueingBasicConsumer consumer, int dequeueTimeout)
        {
            BasicDeliverEventArgs rawMessage;

            var messageDequeued = consumer.Queue.Dequeue(dequeueTimeout, out rawMessage);
            
            if (!messageDequeued)
            {
                return null;
            }

            return rawMessage;
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
        SemaphoreSlim tracksRunningThreads;
        bool noAck;
        int actualConcurrencyLevel;
        readonly ReceiveOptions receiveOptions;
        private readonly IRoutingTopology routingTopology;
        private readonly IChannelProvider channelProvider;
        ushort actualPrefetchCount;
        bool isStopping;

        class ConsumeParams
        {
            public CancellationToken CancellationToken;
            public string Queue;
        }
    }
}