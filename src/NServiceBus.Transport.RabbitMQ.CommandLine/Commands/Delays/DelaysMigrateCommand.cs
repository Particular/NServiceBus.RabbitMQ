namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System;
    using System.CommandLine;
    using System.Globalization;
    using System.Text;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;

    class DelaysMigrateCommand
    {
        const string poisonMessageQueue = "delays-migrate-poison-messages";
        const string timeSentHeader = "NServiceBus.TimeSent";
        const string dateTimeOffsetWireFormat = "yyyy-MM-dd HH:mm:ss:ffffff Z";
        const int indexStartOfDestinationQueue = DelayInfrastructure.MaxNumberOfBitsToUse * 2;

        public static Command CreateCommand()
        {
            var command = new Command("migrate", "Migrate in-flight delayed messages from the v1 delay infrastructure to the v2 delay infrastructure");

            var brokerConnectionBinder = SharedOptions.CreateBrokerConnectionBinderWithOptions(command);

            var routingTopologyTypeOption = SharedOptions.CreateRoutingTopologyTypeOption();
            command.AddOption(routingTopologyTypeOption);

            command.SetHandler(async (brokerConnection, routingTopologyType, console, cancellationToken) =>
            {
                IRoutingTopology routingTopology = routingTopologyType switch
                {
                    RoutingTopologyType.Conventional => new ConventionalRoutingTopology(true, QueueType.Quorum),
                    RoutingTopologyType.Direct => new DirectRoutingTopology(true, QueueType.Quorum),
                    _ => throw new InvalidOperationException()
                };

                var delaysMigrate = new DelaysMigrateCommand(brokerConnection, routingTopology, console);
                await delaysMigrate.Run(cancellationToken).ConfigureAwait(false);
            },
            brokerConnectionBinder, routingTopologyTypeOption, Bind.FromServiceProvider<IConsole>(), Bind.FromServiceProvider<CancellationToken>());

            return command;
        }

        public DelaysMigrateCommand(BrokerConnection brokerConnection, IRoutingTopology routingTopology, IConsole console)
        {
            this.brokerConnection = brokerConnection;
            this.routingTopology = routingTopology;
            this.console = console;
        }

        public Task Run(CancellationToken cancellationToken = default)
        {
            using var connection = brokerConnection.Create();
            using var channel = connection.CreateModel();
            channel.ConfirmSelect();

            for (int currentDelayLevel = DelayInfrastructure.MaxLevel; currentDelayLevel >= 0 && !cancellationToken.IsCancellationRequested; currentDelayLevel--)
            {
                MigrateQueue(channel, currentDelayLevel, cancellationToken);
            }

            return Task.CompletedTask;
        }

        void MigrateQueue(IModel channel, int delayLevel, CancellationToken cancellationToken)
        {
            var currentDelayQueue = $"nsb.delay-level-{delayLevel:00}";
            var messageCount = channel.MessageCount(currentDelayQueue);
            var declaredDestinationQueues = new HashSet<string>();

            if (messageCount > 0)
            {
                console.Write($"Processing {messageCount} messages at delay level {delayLevel:00}. ");

                int skippedMessages = 0;
                int processedMessages = 0;

                for (int i = 0; i < messageCount && !cancellationToken.IsCancellationRequested; i++)
                {
                    var message = channel.BasicGet(currentDelayQueue, false);

                    if (message == null)
                    {
                        // Queue is empty
                        break;
                    }

                    if (MessageIsInvalid(message))
                    {
                        skippedMessages++;

                        if (!poisonQueueCreated)
                        {
                            channel.QueueDeclare(poisonMessageQueue, true, false, false);
                            poisonQueueCreated = true;
                        }

                        channel.BasicPublish(string.Empty, poisonMessageQueue, message.BasicProperties, message.Body);
                        channel.WaitForConfirmsOrDie();
                        channel.BasicAck(message.DeliveryTag, false);

                        continue;
                    }

                    var messageHeaders = message.BasicProperties.Headers;
                    var delayInSeconds = (int)messageHeaders[DelayInfrastructure.DelayHeader];
                    var timeSent = GetTimeSent(message);

                    var (destinationQueue, newRoutingKey, newDelayLevel) = GetNewRoutingKey(delayInSeconds, timeSent, message.RoutingKey, DateTimeOffset.UtcNow);

                    // Make sure the destination queue is bound to the delivery exchange to ensure delivery
                    if (!declaredDestinationQueues.Contains(destinationQueue))
                    {
                        routingTopology.BindToDelayInfrastructure(channel, destinationQueue, DelayInfrastructure.DeliveryExchange, DelayInfrastructure.BindingKey(destinationQueue));
                        declaredDestinationQueues.Add(destinationQueue);
                    }

                    var publishExchange = DelayInfrastructure.LevelName(newDelayLevel);

                    if (messageHeaders != null)
                    {
                        //These headers need to be removed so that they won't be copied to an outgoing message if this message gets forwarded
                        messageHeaders.Remove(DelayInfrastructure.XDeathHeader);
                        messageHeaders.Remove(DelayInfrastructure.XFirstDeathExchangeHeader);
                        messageHeaders.Remove(DelayInfrastructure.XFirstDeathQueueHeader);
                        messageHeaders.Remove(DelayInfrastructure.XFirstDeathReasonHeader);
                    }

                    channel.BasicPublish(publishExchange, newRoutingKey, message.BasicProperties, message.Body);
                    channel.WaitForConfirmsOrDie();
                    channel.BasicAck(message.DeliveryTag, false);
                    processedMessages++;
                }

                console.WriteLine($"{processedMessages} successful, {skippedMessages} skipped.");
            }
            else
            {
                console.WriteLine($"No messages to process at delay level {delayLevel:00}.");
            }
        }

        public static (string DestinationQueue, string NewRoutingKey, int NewDelayLevel) GetNewRoutingKey(int delayInSeconds, DateTimeOffset timeSent, string currentRoutingKey, DateTimeOffset utcNow)
        {
            var originalDeliveryDate = timeSent.AddSeconds(delayInSeconds);
            var newDelayInSeconds = Convert.ToInt32(originalDeliveryDate.Subtract(utcNow).TotalSeconds);
            var destinationQueue = currentRoutingKey[indexStartOfDestinationQueue..];
            var newRoutingKey = DelayInfrastructure.CalculateRoutingKey(newDelayInSeconds, destinationQueue, out int newDelayLevel);

            return (destinationQueue, newRoutingKey, newDelayLevel);
        }

        static DateTimeOffset GetTimeSent(BasicGetResult message)
        {
            var timeSentString = Encoding.UTF8.GetString((byte[])message.BasicProperties.Headers[timeSentHeader]);
            return DateTimeOffset.ParseExact(timeSentString, dateTimeOffsetWireFormat, CultureInfo.InvariantCulture);
        }

        static bool MessageIsInvalid(BasicGetResult? message)
        {
            return message == null
                || message.BasicProperties == null
                || message.BasicProperties.Headers == null
                || !message.BasicProperties.Headers.ContainsKey(DelayInfrastructure.DelayHeader)
                || !message.BasicProperties.Headers.ContainsKey(timeSentHeader);
        }

        readonly BrokerConnection brokerConnection;
        readonly IRoutingTopology routingTopology;
        readonly IConsole console;

        bool poisonQueueCreated = false;
    }
}
