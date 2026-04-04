namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System;
    using System.CommandLine;
    using System.Globalization;
    using System.Text;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;

    class DelaysTransferCommand(BrokerConnection sourceBrokerConnection, BrokerConnection destinationBrokerConnection, IRoutingTopology routingTopology, TextWriter output)
    {
        const string poisonMessageQueue = "delays-transfer-poison-messages";
        const string timeSentHeader = "NServiceBus.TimeSent";
        const string dateTimeOffsetWireFormat = "yyyy-MM-dd HH:mm:ss:ffffff Z";
        const int indexStartOfDestinationQueue = DelayInfrastructure.MaxNumberOfBitsToUse * 2;

        public static Command CreateCommand()
        {
            var command = new Command("transfer", "Transfer delayed messages from one broker to another");

            var (sourceBrokerConnectionBinder, destinationBrokerConnectionBinder) = SharedOptions.CreateSourceAndDestinationBrokerConnectionBindersWithOptions(command);

            var routingTopologyTypeOption = SharedOptions.CreateRoutingTopologyTypeOption();
            command.Options.Add(routingTopologyTypeOption);

            command.SetAction(async (parseResult, cancellationToken) =>
            {
                var sourceBrokerConnection = sourceBrokerConnectionBinder.CreateBrokerConnection(parseResult);
                var destinationBrokerConnection = destinationBrokerConnectionBinder.CreateBrokerConnection(parseResult);
                var routingTopologyType = parseResult.GetValue(routingTopologyTypeOption);

                IRoutingTopology routingTopology = routingTopologyType switch
                {
                    RoutingTopologyType.Conventional => new ConventionalRoutingTopology(true, QueueType.Quorum),
                    RoutingTopologyType.Direct => new DirectRoutingTopology(true, QueueType.Quorum),
                    _ => throw new InvalidOperationException()
                };

                var delaysTransfer = new DelaysTransferCommand(sourceBrokerConnection, destinationBrokerConnection, routingTopology, parseResult.InvocationConfiguration.Output);
                await delaysTransfer.Run(cancellationToken);
            });

            return command;
        }

        async Task Run(CancellationToken cancellationToken)
        {
            await using var sourceConnection = await sourceBrokerConnection.Create(cancellationToken);
            await using var destinationConnection = await destinationBrokerConnection.Create(cancellationToken);

            var createChannelOptions = new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true, outstandingPublisherConfirmationsRateLimiter: null);

            await using var sourceChannel = await sourceConnection.CreateChannelAsync(createChannelOptions, cancellationToken);
            await using var destinationChannel = await destinationConnection.CreateChannelAsync(createChannelOptions, cancellationToken);

            for (int currentDelayLevel = DelayInfrastructure.MaxLevel; currentDelayLevel >= 0 && !cancellationToken.IsCancellationRequested; currentDelayLevel--)
            {
                await TransferQueue(sourceChannel, destinationChannel, currentDelayLevel, cancellationToken);
            }
        }

        async Task TransferQueue(IChannel sourceChannel, IChannel destinationChannel, int delayLevel, CancellationToken cancellationToken)
        {
            var currentDelayQueue = DelayInfrastructure.LevelName(delayLevel);
            var messageCount = await sourceChannel.MessageCountAsync(currentDelayQueue, cancellationToken);
            var declaredDestinationQueues = new HashSet<string>();

            if (messageCount > 0)
            {
                output.Write($"Processing {messageCount} messages at delay level {delayLevel:00}. ");

                int skippedMessages = 0;
                int processedMessages = 0;

                for (int i = 0; i < messageCount && !cancellationToken.IsCancellationRequested; i++)
                {
                    var message = await sourceChannel.BasicGetAsync(currentDelayQueue, false, cancellationToken);

                    if (message is null)
                    {
                        // Queue is empty
                        break;
                    }

                    if (MessageIsInvalid(message))
                    {
                        skippedMessages++;

                        if (!poisonQueueCreated)
                        {
                            await sourceChannel.QueueDeclareAsync(poisonMessageQueue, true, false, false, cancellationToken: cancellationToken);
                            poisonQueueCreated = true;
                        }

                        await sourceChannel.BasicPublishAsync(string.Empty, poisonMessageQueue, false, new BasicProperties(message.BasicProperties), message.Body, cancellationToken: cancellationToken);
                        await sourceChannel.BasicAckAsync(message.DeliveryTag, false, cancellationToken);

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
                        await routingTopology.BindToDelayInfrastructure(destinationChannel, destinationQueue, DelayInfrastructure.DeliveryExchange, DelayInfrastructure.BindingKey(destinationQueue), cancellationToken);
                        declaredDestinationQueues.Add(destinationQueue);
                    }

                    var publishExchange = DelayInfrastructure.LevelName(newDelayLevel);

                    if (messageHeaders is not null)
                    {
                        //These headers need to be removed so that they won't be copied to an outgoing message if this message gets forwarded
                        messageHeaders.Remove(DelayInfrastructure.XDeathHeader);
                        messageHeaders.Remove(DelayInfrastructure.XFirstDeathExchangeHeader);
                        messageHeaders.Remove(DelayInfrastructure.XFirstDeathQueueHeader);
                        messageHeaders.Remove(DelayInfrastructure.XFirstDeathReasonHeader);
                    }

                    await destinationChannel.BasicPublishAsync(publishExchange, newRoutingKey, false, new BasicProperties(message.BasicProperties), message.Body, cancellationToken: cancellationToken);
                    await sourceChannel.BasicAckAsync(message.DeliveryTag, false, cancellationToken);
                    processedMessages++;
                }

                output.WriteLine($"{processedMessages} successful, {skippedMessages} skipped.");
            }
            else
            {
                output.WriteLine($"No messages to process at delay level {delayLevel:00}.");
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
            var timeSentHeaderValue = message.BasicProperties.Headers?[timeSentHeader];
            var timeSentHeaderBytes = Array.Empty<byte>();

            if (timeSentHeaderValue is not null)
            {
                timeSentHeaderBytes = (byte[])timeSentHeaderValue;
            }

            var timeSentString = Encoding.UTF8.GetString(timeSentHeaderBytes);
            return DateTimeOffset.ParseExact(timeSentString, dateTimeOffsetWireFormat, CultureInfo.InvariantCulture);
        }

        static bool MessageIsInvalid(BasicGetResult? message) =>
            message?.BasicProperties?.Headers is null
            || !message.BasicProperties.Headers.ContainsKey(DelayInfrastructure.DelayHeader)
            || !message.BasicProperties.Headers.ContainsKey(timeSentHeader);

        bool poisonQueueCreated = false;
    }
}
