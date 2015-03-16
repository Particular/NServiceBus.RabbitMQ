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


    class RabbitMqDequeueStrategy : IDequeueMessages, IDisposable
    {   
        public RabbitMqDequeueStrategy(IManageRabbitMqConnections connectionManager, RepeatedFailuresOverTimeCircuitBreaker circuitBreaker, ReceiveOptions receiveOptions)
        {
            this.connectionManager = connectionManager;
            this.circuitBreaker = circuitBreaker;
            this.receiveOptions = receiveOptions;
        }

        public void Init(Address address, TransactionSettings transactionSettings, Func<TransportMessage, bool> tryProcessMessage, Action<TransportMessage, Exception> endProcessMessage)
        {
            this.tryProcessMessage = tryProcessMessage;
            this.endProcessMessage = endProcessMessage;
            workQueue = address.Queue;

            noAck = !transactionSettings.IsTransactional;

            if (receiveOptions.PurgeOnStartup)
            {
                Purge();
            }
        }

        public void Start(int maximumConcurrencyLevel)
        {
            isStopping = false;

            if (receiveOptions.DefaultPrefetchCount > 0)
            {
                actualPrefetchCount = receiveOptions.DefaultPrefetchCount;
            }
            else
            {
                actualPrefetchCount = Convert.ToUInt16(maximumConcurrencyLevel);

                Logger.InfoFormat("No prefetch count configured, defaulting to {0} (the configured concurrency level)", actualPrefetchCount);                
            }


            var secondaryReceiveSettings = receiveOptions.GetSettings(workQueue);

            actualConcurrencyLevel = maximumConcurrencyLevel + secondaryReceiveSettings.MaximumConcurrencyLevel;

            tokenSource = new CancellationTokenSource();

            // We need to add an extra one because if we fail and the count is at zero already, it doesn't allow us to add one more.
            tracksRunningThreads = new SemaphoreSlim(actualConcurrencyLevel, actualConcurrencyLevel);

            for (var i = 0; i < maximumConcurrencyLevel; i++)
            {
                StartConsumer(workQueue);
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


        /// <summary>
        ///     Stops the dequeuing of messages.
        /// </summary>
        public void Stop()
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

            WaitForThreadsToStop();

            tokenSource = null;
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
                        StartConsumer(queue);
                    }
                }, TaskContinuationOptions.OnlyOnFaulted);
        }

        void ConsumeMessages(object state)
        {
            if (!tracksRunningThreads.Wait(TimeSpan.FromSeconds(1)))
            {
                return;
            }

            try
            {
                var parameters = (ConsumeParams)state;
                var connection = connectionManager.GetConsumeConnection();

                using (var channel = connection.CreateModel())
                {
                    channel.BasicQos(0, actualPrefetchCount, false);

                    var consumer = new QueueingBasicConsumer(channel);

                    channel.BasicConsume(parameters.Queue, noAck,receiveOptions.ConsumerTag, consumer);

                    circuitBreaker.Success();

                    while (!parameters.CancellationToken.IsCancellationRequested)
                    {
                        Exception exception = null;
                        var message = DequeueMessage(consumer, receiveOptions.DequeueTimeout);

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
                                transportMessage = receiveOptions.Converter.ToTransportMessage(message);
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
                            exception = ex;

                            if (!noAck)
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
                channel.QueuePurge(workQueue);
            }
        }



        static ILog Logger = LogManager.GetLogger(typeof(RabbitMqDequeueStrategy));

        RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;

        bool noAck;
        SemaphoreSlim tracksRunningThreads;
        Action<TransportMessage, Exception> endProcessMessage;
        CancellationTokenSource tokenSource;
        Func<TransportMessage, bool> tryProcessMessage;
        string workQueue;
        int actualConcurrencyLevel;
        readonly IManageRabbitMqConnections connectionManager;
        readonly ReceiveOptions receiveOptions;
        ushort actualPrefetchCount;
        bool isStopping;


        class ConsumeParams
        {
            public CancellationToken CancellationToken;
            public string Queue;
        }
    }
}