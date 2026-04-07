namespace NServiceBus.Transport.RabbitMQ.CommandLine;

using System.Globalization;
using System.Text;
using global::RabbitMQ.Client;

static class DelayCommandHelpers
{
    const string timeSentHeader = "NServiceBus.TimeSent";
    const string dateTimeOffsetWireFormat = "yyyy-MM-dd HH:mm:ss:ffffff Z";
    const int indexStartOfDestinationQueue = DelayInfrastructure.MaxNumberOfBitsToUse * 2;

    public static async Task TransferMessages(bool sourceIsV1, IChannel sourceChannel, IChannel destinationChannel, string poisonMessageQueue, IRoutingTopology routingTopology, TextWriter output, CancellationToken cancellationToken = default)
    {
        HashSet<string> destinationQueues = [];

        for (int currentDelayLevel = DelayInfrastructure.MaxLevel; currentDelayLevel >= 0 && !cancellationToken.IsCancellationRequested; currentDelayLevel--)
        {
            await TransferQueue(sourceIsV1, sourceChannel, destinationChannel, currentDelayLevel, poisonMessageQueue, routingTopology, destinationQueues, output, cancellationToken);
        }
    }

    static async Task TransferQueue(bool sourceIsV1, IChannel sourceChannel, IChannel destinationChannel, int delayLevel, string poisonMessageQueue, IRoutingTopology routingTopology, HashSet<string> destinationQueues, TextWriter output, CancellationToken cancellationToken)
    {
        var currentDelayQueue = sourceIsV1 ? $"nsb.delay-level-{delayLevel:00}" : DelayInfrastructure.LevelName(delayLevel);
        var messageCount = await sourceChannel.MessageCountAsync(currentDelayQueue, cancellationToken);

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

                    await sourceChannel.QueueDeclareAsync(poisonMessageQueue, true, false, false, cancellationToken: cancellationToken);
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
                if (!destinationQueues.Contains(destinationQueue))
                {
                    await routingTopology.BindToDelayInfrastructure(destinationChannel, destinationQueue, DelayInfrastructure.DeliveryExchange, DelayInfrastructure.BindingKey(destinationQueue), cancellationToken);
                    destinationQueues.Add(destinationQueue);
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
}
