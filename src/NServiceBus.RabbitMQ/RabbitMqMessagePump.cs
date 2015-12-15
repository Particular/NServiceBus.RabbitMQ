namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using NServiceBus.Extensibility;

    class RabbitMqMessagePump : IPushMessages
    {
        public RabbitMqMessagePump(IManageRabbitMqConnections connectionManager)
        {
            this.connectionManager = connectionManager;
        }

        public Task Init(Func<PushContext, Task> pipe, CriticalError criticalError, PushSettings settings)
        {
            this.pipe = pipe;
            this.criticalError = criticalError;
            this.settings = settings;

            noAck = settings.RequiredTransactionMode == TransportTransactionMode.None;

            if (settings.PurgeOnStartup)
            {
                Purge();
            }

            return Task.FromResult(0);
        }

        public void Start(PushRuntimeSettings limitations)
        {
            tokenSource = new CancellationTokenSource();

            for (var i = 0; i < limitations.MaxConcurrency; i++)
            {
                StartConsumer(settings.InputQueue);
            }
            
        }


        public Task Stop()
        {
            return Task.FromResult(0);
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
                        // TODO: Logging and circuitbreakers
                        //Logger.Error("Failed to receive messages from " + queue, t.Exception);
                        //circuitBreaker.Failure(ex);
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
            var parameters = (ConsumeParams)state;
            var consumeConnection = connectionManager.GetConsumeConnection();
            using (var channel = consumeConnection.CreateModel())
            {
                channel.BasicQos(0, prefetchCount, false);

                var consumer = new QueueingBasicConsumer(channel);

                channel.BasicConsume(settings.InputQueue, noAck, consumerTag, consumer);

                // Indicate success on circuitbreaker

                while (!parameters.CancellationToken.IsCancellationRequested)
                {
                    var rabbitMessage = DequeueRabbitMessage(consumer, dequeueTimeout);

                    if (rabbitMessage == null)
                    {
                        continue;
                    }

                    try
                    {
                        var processedOk = false;

                        

                        IncomingMessage transportMessage = ConvertToTransportMessage(rabbitMessage);

                        if (transportMessage != null)
                        {
                            var pushContext = new PushContext(transportMessage.MessageId, transportMessage.Headers, transportMessage.BodyStream, new TransportTransaction(), new ContextBag());
                            pipe(pushContext).GetAwaiter().GetResult();
                        }

                        if (!noAck)
                        {
                            if (processedOk)
                            {
                                channel.BasicAck(rabbitMessage.DeliveryTag, false);
                            }
                            else
                            {
                                channel.BasicReject(rabbitMessage.DeliveryTag, true);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        if (!noAck)
                        {
                            channel.BasicReject(rabbitMessage.DeliveryTag, true);
                        }
                        throw;
                    }

                }
            }
        }

        static BasicDeliverEventArgs DequeueRabbitMessage(QueueingBasicConsumer consumer, int dequeueTimeout)
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

        IManageRabbitMqConnections connectionManager;
        Func<PushContext, Task> pipe;
        CriticalError criticalError;
        PushSettings settings;
        CancellationTokenSource tokenSource;

        bool noAck;

        class ConsumeParams
        {
            public CancellationToken CancellationToken;
            public string Queue;
        }
    }
}