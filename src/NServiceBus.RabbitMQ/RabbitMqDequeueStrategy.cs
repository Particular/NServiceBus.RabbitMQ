namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using CircuitBreakers;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using Logging;
    using Unicast.Transport;

    /// <summary>
    ///     Default implementation of <see cref="IDequeueMessages" /> for RabbitMQ.
    /// </summary>
    class RabbitMqDequeueStrategy : IDequeueMessages, IDisposable
    {
        readonly IManageRabbitMqConnections connectionManager;
        readonly SecondaryReceiveConfiguration secondaryReceiveConfiguration;

        /// <summary>
        ///     The number of messages to allow the RabbitMq client to pre-fetch from the broker
        /// </summary>
        public ushort PrefetchCount { get; set; }

        public RabbitMqDequeueStrategy(IManageRabbitMqConnections connectionManager, CriticalError criticalError, Configure config, SecondaryReceiveConfiguration secondaryReceiveConfiguration)
        {
            this.connectionManager = connectionManager;
            this.secondaryReceiveConfiguration = secondaryReceiveConfiguration;
            circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("RabbitMqConnectivity",
            TimeSpan.FromMinutes(2),
            ex => criticalError.Raise("Repeated failures when communicating with the RabbitMq broker", ex),
            TimeSpan.FromSeconds(5));
            purgeOnStartup = config.PurgeOnStartup();
        }

        /// <summary>
        ///     Initializes the <see cref="IDequeueMessages" />.
        /// </summary>
        /// <param name="address">The address to listen on.</param>
        /// <param name="transactionSettings">The <see cref="TransactionSettings" /> to be used by <see cref="IDequeueMessages" />.</param>
        /// <param name="tryProcessMessage">Called when a message has been dequeued and is ready for processing.</param>
        /// <param name="endProcessMessage">
        ///     Needs to be called by <see cref="IDequeueMessages" /> after the message has been
        ///     processed regardless if the outcome was successful or not.
        /// </param>
        public void Init(Address address, TransactionSettings transactionSettings, Func<TransportMessage, bool> tryProcessMessage, Action<TransportMessage, Exception> endProcessMessage)
        {
            this.tryProcessMessage = tryProcessMessage;
            this.endProcessMessage = endProcessMessage;
            workQueue = address.Queue;

            autoAck = !transactionSettings.IsTransactional;

            if (purgeOnStartup)
            {
                Purge();
            }
        }

        /// <summary>
        ///     Starts the dequeuing of message using the specified <paramref name="maximumConcurrencyLevel" />.
        /// </summary>
        /// <param name="maximumConcurrencyLevel">
        ///     Indicates the maximum concurrency level this <see cref="IDequeueMessages" /> is
        ///     able to support.
        /// </param>
        public void Start(int maximumConcurrencyLevel)
        {
            var secondaryReceiveSettings = secondaryReceiveConfiguration.GetSettings(workQueue);

            var actualConcurrencyLevel = maximumConcurrencyLevel +
                secondaryReceiveSettings.MaximumConcurrencyLevel * secondaryReceiveSettings.SecondaryQueues.Count;

            tokenSource = new CancellationTokenSource();
            
            // We need to add an extra one because if we fail and the count is at zero already, it doesn't allow us to add one more.
            countdownEvent = new CountdownEvent(actualConcurrencyLevel + 1);

            for (var i = 0; i < maximumConcurrencyLevel; i++)
            {
                StartConsumer(workQueue);
            }

            foreach (var secondaryReceiveQueue in secondaryReceiveSettings.SecondaryQueues)
            {
                for (var i = 0; i < secondaryReceiveSettings.MaximumConcurrencyLevel; i++)
                {
                    StartConsumer(secondaryReceiveQueue);
                }

                Logger.InfoFormat("Secondary receiver for queue '{0}' initiated with concurrency '{1}'",secondaryReceiveQueue,secondaryReceiveSettings.MaximumConcurrencyLevel);
            }
        }


        /// <summary>
        ///     Stops the dequeuing of messages.
        /// </summary>
        public void Stop()
        {
            if (tokenSource == null)
            {
                return;
            }

            tokenSource.Cancel();
            countdownEvent.Signal();
            countdownEvent.Wait();
        }

        public void Dispose()
        {
            // Injected
        }

        void StartConsumer(string queue)
        {
            var token = tokenSource.Token;
            Task.Factory
                .StartNew(ConsumeMessages, new ConsumeParams { Queue = queue, CancellationToken = token }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default)
                .ContinueWith(t =>
                {
                    t.Exception.Handle(ex =>
                    {
                        circuitBreaker.Failure(ex);
                        return true;
                    });

                    if (!tokenSource.IsCancellationRequested)
                    {
                        if (countdownEvent.TryAddCount())
                        {
                            StartConsumer(queue);
                        }
                    }
                }, TaskContinuationOptions.OnlyOnFaulted);
        }

        void ConsumeMessages(object state)
        {
            try
            {
                var parameters = (ConsumeParams)state;
                var connection = connectionManager.GetConsumeConnection();

                using (var channel = connection.CreateModel())
                {
                    channel.BasicQos(0, PrefetchCount, false);

                    var consumer = new QueueingBasicConsumer(channel);

                    channel.BasicConsume(parameters.Queue, autoAck, consumer);

                    circuitBreaker.Success();

                    while (!parameters.CancellationToken.IsCancellationRequested)
                    {
                        Exception exception = null;
                        var message = DequeueMessage(consumer);

                        if (message == null)
                        {
                            continue;
                        }

                        TransportMessage transportMessage = null;

                        try
                        {
                            var messageProcessedOk = false;

                            try
                            {
                                transportMessage = RabbitMqTransportMessageExtensions.ToTransportMessage(message);
                            }
                            catch (Exception ex)
                            {
                                Logger.Error("Poison message detected, deliveryTag: " + message.DeliveryTag, ex);

                                //just ack the poison message to avoid getting stuck
                                messageProcessedOk = true;
                            }

                            if (transportMessage != null)
                            {
                                messageProcessedOk = tryProcessMessage(transportMessage);
                            }

                            if (!autoAck)
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
                            exception = ex;

                            if (!autoAck)
                            {
                                channel.BasicReject(message.DeliveryTag, true);
                            }
                        }
                        finally
                        {
                            endProcessMessage(transportMessage, exception);
                        }
                    }
                }
            }
            catch (IOException)
            {
                //Unable to write data to the transport connection: An existing connection was forcibly closed by the remote host.
                //This exception is expected because we are shutting down!
            }
            finally
            {
                countdownEvent.Signal();
            }
        }

        static BasicDeliverEventArgs DequeueMessage(QueueingBasicConsumer consumer)
        {
            BasicDeliverEventArgs rawMessage = null;

            var messageDequeued = false;

            try
            {
                messageDequeued = consumer.Queue.Dequeue(1000, out rawMessage);
            }
            catch (EndOfStreamException)
            {
                // If no items are present and the queue is in a closed
                // state, or if at any time while waiting the queue
                // transitions to a closed state (by a call to Close()), this
                // method will throw EndOfStreamException.
            }

            if (!messageDequeued)
            {
                return null;
            }

            return rawMessage;
        }

        void Purge()
        {
            using (var channel = connectionManager.GetAdministrationConnection().CreateModel())
            {
                channel.QueuePurge(workQueue);
            }
        }

        static ILog Logger = LogManager.GetLogger(typeof(RabbitMqDequeueStrategy));

        RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;
    
        bool autoAck;
        CountdownEvent countdownEvent;
        Action<TransportMessage, Exception> endProcessMessage;
        CancellationTokenSource tokenSource;
        Func<TransportMessage, bool> tryProcessMessage;
        string workQueue;
        bool purgeOnStartup;


        class ConsumeParams
        {
            public CancellationToken CancellationToken;
            public string Queue;
        }
    }
}