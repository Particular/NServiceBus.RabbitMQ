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
                await delaysMigrate.Run(cancellationToken);
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

        public async Task Run(CancellationToken cancellationToken = default)
        {
            using var connection = await brokerConnection.Create(cancellationToken);
            using var channel = await connection.CreateChannelAsync(cancellationToken);
            await channel.ConfirmSelectAsync(cancellationToken);

            for (int currentDelayLevel = DelayInfrastructure.MaxLevel; currentDelayLevel >= 0 && !cancellationToken.IsCancellationRequested; currentDelayLevel--)
            {
                await MigrateQueue(channel, currentDelayLevel, cancellationToken);
            }
        }

        async Task MigrateQueue(IChannel channel, int delayLevel, CancellationToken cancellationToken)
        {
            var currentDelayQueue = $"nsb.delay-level-{delayLevel:00}";
            var messageCount = await channel.MessageCountAsync(currentDelayQueue, cancellationToken);
            var declaredDestinationQueues = new HashSet<string>();

            if (messageCount > 0)
            {
                console.Write($"Processing {messageCount} messages at delay level {delayLevel:00}. ");

                int skippedMessages = 0;
                int processedMessages = 0;

                for (int i = 0; i < messageCount && !cancellationToken.IsCancellationRequested; i++)
                {
                    var message = await channel.BasicGetAsync(currentDelayQueue, false, cancellationToken);

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
                            await channel.QueueDeclareAsync(poisonMessageQueue, true, false, false, cancellationToken: cancellationToken);
                            poisonQueueCreated = true;
                        }

                        await channel.BasicPublishAsync(string.Empty, poisonMessageQueue, new BasicProperties(message.BasicProperties), message.Body, cancellationToken: cancellationToken);
                        await channel.WaitForConfirmsOrDieAsync(cancellationToken);
                        await channel.BasicAckAsync(message.DeliveryTag, false, cancellationToken);

                        continue;
                    }

                    var messageHeaders = message.BasicProperties.Headers;

                    var delayInSeconds = 0;

                    if (messageHeaders is not null)
                    {
                        var headerValue = messageHeaders[DelayInfrastructure.DelayHeader];

                        if (headerValue is not null)
                        {
                            delayInSeconds = (int)headerValue;
                        }
                    }

                    var timeSent = GetTimeSent(message);

                    var (destinationQueue, newRoutingKey, newDelayLevel) = GetNewRoutingKey(delayInSeconds, timeSent, message.RoutingKey, DateTimeOffset.UtcNow);

                    // Make sure the destination queue is bound to the delivery exchange to ensure delivery
                    if (!declaredDestinationQueues.Contains(destinationQueue))
                    {
                        await routingTopology.BindToDelayInfrastructure(channel, destinationQueue, DelayInfrastructure.DeliveryExchange, DelayInfrastructure.BindingKey(destinationQueue), cancellationToken);
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

                    await channel.BasicPublishAsync(publishExchange, newRoutingKey, new BasicProperties(message.BasicProperties), message.Body, cancellationToken: cancellationToken);
                    await channel.WaitForConfirmsOrDieAsync(cancellationToken);
                    await channel.BasicAckAsync(message.DeliveryTag, false, cancellationToken);
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
            var destinationQueue = currentRoutingKey.Substring(currentRoutingKey.LastIndexOf('.') + 1);
            var newRoutingKey = DelayInfrastructure.CalculateRoutingKey(newDelayInSeconds, destinationQueue, out int newDelayLevel);

            return (destinationQueue, newRoutingKey, newDelayLevel);
        }

        static DateTimeOffset GetTimeSent(BasicGetResult message)
        {
            var timeSentHeaderValue = message.BasicProperties.Headers?[timeSentHeader];
            var timeSentHeaderBytes = Array.Empty<byte>();

            if (timeSentHeaderValue is not null)
            {
                timeSentHeaderBytes = (byte[])timeSentHeaderValue;
            }

            var timeSentString = Encoding.UTF8.GetString(timeSentHeaderBytes);
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
