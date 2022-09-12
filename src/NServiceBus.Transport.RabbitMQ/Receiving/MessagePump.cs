namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using global::RabbitMQ.Client.Exceptions;
    using Logging;

    sealed class MessagePump : IPushMessages, IDisposable
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(MessagePump));
        static readonly TransportTransaction transportTransaction = new TransportTransaction();

        readonly ConnectionFactory connectionFactory;
        readonly MessageConverter messageConverter;
        readonly string consumerTag;
        readonly ChannelProvider channelProvider;
        readonly QueuePurger queuePurger;
        readonly TimeSpan timeToWaitBeforeTriggeringCircuitBreaker;
        readonly int prefetchMultiplier;
        readonly ushort overriddenPrefetchCount;
        readonly TimeSpan retryDelay;

        // Init
        Func<MessageContext, Task> onMessage;
        Func<ErrorContext, Task<ErrorHandleResult>> onError;
        PushSettings settings;
        CriticalError criticalError;
        string name;
        MessagePumpConnectionFailedCircuitBreaker circuitBreaker;
        TaskScheduler exclusiveScheduler;

        // Start
        int maxConcurrency;
        SemaphoreSlim semaphore;
        CancellationTokenSource messageProcessing;
        IConnection connection;

        // Stop
        TaskCompletionSource<bool> connectionShutdownCompleted;

        public MessagePump(ConnectionFactory connectionFactory, MessageConverter messageConverter, string consumerTag, ChannelProvider channelProvider, QueuePurger queuePurger, TimeSpan timeToWaitBeforeTriggeringCircuitBreaker, int prefetchMultiplier, ushort overriddenPrefetchCount, TimeSpan retryDelay)
        {
            this.connectionFactory = connectionFactory;
            this.messageConverter = messageConverter;
            this.consumerTag = consumerTag;
            this.channelProvider = channelProvider;
            this.queuePurger = queuePurger;
            this.timeToWaitBeforeTriggeringCircuitBreaker = timeToWaitBeforeTriggeringCircuitBreaker;
            this.prefetchMultiplier = prefetchMultiplier;
            this.overriddenPrefetchCount = overriddenPrefetchCount;
            this.retryDelay = retryDelay;
        }

        public Task Init(Func<MessageContext, Task> onMessage, Func<ErrorContext, Task<ErrorHandleResult>> onError, CriticalError criticalError, PushSettings settings)
        {
            this.onMessage = onMessage;
            this.onError = onError;
            this.settings = settings;
            this.criticalError = criticalError;

            name = $"{settings.InputQueue} MessagePump";

            circuitBreaker = new MessagePumpConnectionFailedCircuitBreaker(name, timeToWaitBeforeTriggeringCircuitBreaker, criticalError);

            exclusiveScheduler = new ConcurrentExclusiveSchedulerPair().ExclusiveScheduler;

            if (settings.PurgeOnStartup)
            {
                queuePurger.Purge(settings.InputQueue);
            }

            return Task.CompletedTask;
        }

        public void Start(PushRuntimeSettings limitations)
        {
            maxConcurrency = limitations.MaxConcurrency;
            semaphore = new SemaphoreSlim(limitations.MaxConcurrency, limitations.MaxConcurrency);
            messageProcessing = new CancellationTokenSource();

            ConnectToBroker();
        }

        void ConnectToBroker()
        {
            connection = connectionFactory.CreateConnection($"{settings.InputQueue} MessagePump");
            connection.ConnectionShutdown += Connection_ConnectionShutdown;

            long prefetchCount;

            if (overriddenPrefetchCount > 0)
            {
                prefetchCount = overriddenPrefetchCount;

                if (prefetchCount < maxConcurrency)
                {
                    Logger.Warn($"The specified prefetch count '{prefetchCount}' is smaller than the specified maximum concurrency '{maxConcurrency}'. The maximum concurrency value will be used as the prefetch count instead.");
                    prefetchCount = maxConcurrency;
                }
            }
            else
            {
                prefetchCount = (long)maxConcurrency * prefetchMultiplier;
            }

            var channel = connection.CreateModel();
            channel.ModelShutdown += Channel_ModelShutdown;
            channel.BasicQos(0, (ushort)Math.Min(prefetchCount, ushort.MaxValue), false);

            var consumer = new EventingBasicConsumer(channel);
            consumer.ConsumerCancelled += Consumer_ConsumerCancelled;
            consumer.Registered += Consumer_Registered;
            consumer.Received += Consumer_Received;

            channel.BasicConsume(settings.InputQueue, false, consumerTag, consumer);
        }

        public async Task Stop()
        {
            messageProcessing.Cancel();

            while (semaphore.CurrentCount != maxConcurrency)
            {
                await Task.Delay(50).ConfigureAwait(false);
            }

            connectionShutdownCompleted = new TaskCompletionSource<bool>();

            if (connection.IsOpen)
            {
                connection.Close();
            }
            else
            {
                connectionShutdownCompleted.SetResult(true);
            }

            await connectionShutdownCompleted.Task.ConfigureAwait(false);
        }

        void Consumer_Registered(object sender, ConsumerEventArgs e)
        {
            circuitBreaker.Success();
        }

        void Connection_ConnectionShutdown(object sender, ShutdownEventArgs e)
        {
            if (e.Initiator == ShutdownInitiator.Application && e.ReplyCode == 200)
            {
                connectionShutdownCompleted?.TrySetResult(true);
            }
            else if (circuitBreaker.Disarmed)
            {
                //log entry handled by event handler registered in ConnectionFactory
                circuitBreaker.Failure(new Exception(e.ToString()));
                _ = Task.Run(() => Reconnect());
            }
            else
            {
                Logger.WarnFormat("'{0}' connection shutdown while reconnect already in progress: {1}", name, e);
            }
        }

        void Channel_ModelShutdown(object sender, ShutdownEventArgs e)
        {
            if (e.Initiator == ShutdownInitiator.Application)
            {
                return;
            }

            if (e.Initiator == ShutdownInitiator.Peer && e.ReplyCode == 404)
            {
                return;
            }

            if (circuitBreaker.Disarmed)
            {
                Logger.WarnFormat("'{0}' channel shutdown: {1}", name, e);
                circuitBreaker.Failure(new Exception(e.ToString()));
                _ = Task.Run(() => Reconnect());
            }
            else
            {
                Logger.WarnFormat("'{0}' channel shutdown while reconnect already in progress: {1}", name, e);
            }
        }

        void Consumer_ConsumerCancelled(object sender, ConsumerEventArgs e)
        {
            var consumer = (EventingBasicConsumer)sender;

            if (consumer.Model.IsOpen && connection.IsOpen)
            {
                if (circuitBreaker.Disarmed)
                {
                    Logger.WarnFormat("'{0}' consumer canceled by broker", name);
                    circuitBreaker.Failure(new Exception($"'{name}' consumer canceled by broker"));
                    _ = Task.Run(() => Reconnect());
                }
                else
                {
                    Logger.WarnFormat("'{0}' consumer canceled by broker while reconnect already in progress", name);
                }
            }
        }

        async Task Reconnect()
        {
            try
            {
                var oldConnection = connection;

                while (true)
                {
                    Logger.InfoFormat("'{0}': Attempting to reconnect in {1} seconds.", name, retryDelay.TotalSeconds);

                    await Task.Delay(retryDelay, messageProcessing.Token).ConfigureAwait(false);

                    try
                    {
                        ConnectToBroker();
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.InfoFormat("'{0}': Reconnecting to the broker failed: {1}", name, ex);
                    }
                }
                Logger.InfoFormat("'{0}': Connection to the broker reestablished successfully.", name);

                if (oldConnection.IsOpen)
                {
                    oldConnection.Close();
                }
                oldConnection.Dispose();
            }
            catch (OperationCanceledException ex) when (messageProcessing.Token.IsCancellationRequested)
            {
                Logger.DebugFormat("'{0}': Reconnection canceled since the transport is being stopped: {1}", name, ex);
            }
            catch (Exception ex)
            {
                Logger.WarnFormat("'{0}': Unexpected error while reconnecting: '{1}'", name, ex);
            }
        }

        async void Consumer_Received(object sender, BasicDeliverEventArgs eventArgs)
        {
            if (messageProcessing.IsCancellationRequested)
            {
                return;
            }

            var consumer = (EventingBasicConsumer)sender;

            var eventRaisingThreadId = Thread.CurrentThread.ManagedThreadId;

            var messageBody = eventArgs.Body.ToArray();

            var eventArgsCopy = new BasicDeliverEventArgs(
                consumerTag: eventArgs.ConsumerTag,
                deliveryTag: eventArgs.DeliveryTag,
                redelivered: eventArgs.Redelivered,
                exchange: eventArgs.Exchange,
                routingKey: eventArgs.RoutingKey,
                properties: eventArgs.BasicProperties,
                body: messageBody
            );

            try
            {
                await semaphore.WaitAsync(messageProcessing.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                // The current thread will be the event-raising thread if either:
                //
                // a) the semaphore was entered synchronously (did not have to wait).
                // b) the event was raised on a thread pool thread,
                //    and the semaphore was entered asynchronously (had to wait),
                //    and the continuation happened to be scheduled back onto the same thread.
                if (Thread.CurrentThread.ManagedThreadId == eventRaisingThreadId)
                {
                    // In RabbitMQ.Client 4.1.0, the event is raised by reusing a single, explicitly created thread,
                    // so we are in scenario (a) described above.
                    // We must yield to allow the thread to raise more events while we handle this one,
                    // otherwise we will never process messages concurrently.
                    //
                    // If a future version of RabbitMQ.Client changes its threading model, then either:
                    //
                    // 1) we are in scenario (a), but we *may not* need to yield.
                    //    E.g. the client may raise the event on a new, explicitly created thread each time.
                    // 2) we cannot tell whether we are in scenario (a) or scenario (b).
                    //    E.g. the client may raise the event on a thread pool thread.
                    //
                    // In both cases, we cannot tell whether we need to yield or not, so we must yield.
                    await Task.Yield();
                }

                await Process(consumer, eventArgsCopy, messageBody).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Warn("Failed to process message. Returning message to queue...", ex);
                await consumer.Model.BasicRejectAndRequeueIfOpen(eventArgs.DeliveryTag, exclusiveScheduler).ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
            }
        }

        async Task Process(EventingBasicConsumer consumer, BasicDeliverEventArgs message, byte[] messageBody)
        {
            Dictionary<string, string> headers;

            try
            {
                headers = messageConverter.RetrieveHeaders(message);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to retrieve headers from poison message. Moving message to queue '{settings.ErrorQueue}'...", ex);
                await MovePoisonMessage(consumer, message, settings.ErrorQueue).ConfigureAwait(false);

                return;
            }

            string messageId;

            try
            {
                messageId = messageConverter.RetrieveMessageId(message, headers);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to retrieve ID from poison message. Moving message to queue '{settings.ErrorQueue}'...", ex);
                await MovePoisonMessage(consumer, message, settings.ErrorQueue).ConfigureAwait(false);

                return;
            }

            using (var tokenSource = new CancellationTokenSource())
            {
                var processed = false;
                var errorHandled = false;
                var numberOfDeliveryAttempts = 0;

                while (!processed && !errorHandled)
                {
                    try
                    {
                        var contextBag = new ContextBag();
                        contextBag.Set(message);

                        var messageContext = new MessageContext(messageId, headers, messageBody ?? new byte[0], transportTransaction, tokenSource, contextBag);

                        await onMessage(messageContext).ConfigureAwait(false);
                        processed = true;
                    }
                    catch (Exception exception)
                    {
                        ++numberOfDeliveryAttempts;
                        headers = messageConverter.RetrieveHeaders(message);
                        var contextBag = new ContextBag();
                        contextBag.Set(message);

                        var errorContext = new ErrorContext(exception, headers, messageId, messageBody ?? new byte[0], transportTransaction, numberOfDeliveryAttempts, contextBag);

                        try
                        {
                            errorHandled = await onError(errorContext).ConfigureAwait(false) == ErrorHandleResult.Handled;

                            if (!errorHandled)
                            {
                                headers = messageConverter.RetrieveHeaders(message);
                            }
                        }
                        catch (Exception ex)
                        {
                            criticalError.Raise($"Failed to execute recoverability policy for message with native ID: `{messageId}`", ex);
                            await consumer.Model.BasicRejectAndRequeueIfOpen(message.DeliveryTag, exclusiveScheduler).ConfigureAwait(false);

                            return;
                        }
                    }
                }

                if (processed && tokenSource.IsCancellationRequested)
                {
                    await consumer.Model.BasicRejectAndRequeueIfOpen(message.DeliveryTag, exclusiveScheduler).ConfigureAwait(false);
                }
                else
                {
                    try
                    {
                        await consumer.Model.BasicAckSingle(message.DeliveryTag, exclusiveScheduler).ConfigureAwait(false);
                    }
                    catch (AlreadyClosedException ex)
                    {
                        Logger.Warn($"Failed to acknowledge message '{messageId}' because the channel was closed. The message was returned to the queue.", ex);
                    }
                }
            }
        }

        async Task MovePoisonMessage(EventingBasicConsumer consumer, BasicDeliverEventArgs message, string queue)
        {
            try
            {
                var channel = channelProvider.GetPublishChannel();

                try
                {
                    await channel.RawSendInCaseOfFailure(queue, message.Body, message.BasicProperties).ConfigureAwait(false);
                }
                finally
                {
                    channelProvider.ReturnPublishChannel(channel);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to move poison message to queue '{queue}'. Returning message to original queue...", ex);
                await consumer.Model.BasicRejectAndRequeueIfOpen(message.DeliveryTag, exclusiveScheduler).ConfigureAwait(false);

                return;
            }

            try
            {
                await consumer.Model.BasicAckSingle(message.DeliveryTag, exclusiveScheduler).ConfigureAwait(false);
            }
            catch (AlreadyClosedException ex)
            {
                Logger.Warn($"Failed to acknowledge poison message because the channel was closed. The message was sent to queue '{queue}' but also returned to the original queue.", ex);
            }
        }

        public void Dispose()
        {
            circuitBreaker?.Dispose();
            semaphore?.Dispose();
            messageProcessing?.Dispose();
            connection?.Dispose();
        }
    }
}
