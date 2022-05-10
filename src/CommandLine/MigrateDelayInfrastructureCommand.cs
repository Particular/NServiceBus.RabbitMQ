namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System;
    using System.CommandLine;
    using System.Globalization;
    using System.Text;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Exceptions;

    public class MigrateDelayInfrastructureCommand
    {
        const string DelayInSecondsHeader = "NServiceBus.Transport.RabbitMQ.DelayInSeconds";
        const string TimeSentHeader = "NServiceBus.TimeSent";
        const string DateTimeOffsetWireFormat = "yyyy-MM-dd HH:mm:ss:ffffff Z";
        const int NumberOfDelayLevelQueues = 27;

        public static Command CreateCommand()
        {
            var useNonDurableEntitiesOption = new Option<bool>(
                 name: "--UseNonDurableEntities",
                 description: $"Create non-durable endpoint queues and exchanges");

            useNonDurableEntitiesOption.AddAlias("-n");

            var runUntilCancelled = new Option<bool>(
                 name: "--RunUntilCancelled",
                 description: $"The migration script will run until the script is cancelled");

            runUntilCancelled.AddAlias("-r");

            var migrateCommand = new Command("migrate-delay-infrastructure", "Migrate existing delay queues and in-flight delayed messages to the latest infrustructure.");
            migrateCommand.AddOption(SharedOptions.ConnectionString);
            migrateCommand.AddOption(useNonDurableEntitiesOption);
            migrateCommand.AddOption(runUntilCancelled);

            migrateCommand.SetHandler(async (bool useNonDurableEntities, string connectionString, bool runUntilCancelled, CancellationToken cancellationToken) =>
            {
                var migrationProcess = new MigrateDelayInfrastructureCommand();
                await migrationProcess.Run(!useNonDurableEntities, connectionString, runUntilCancelled, cancellationToken).ConfigureAwait(false);
            }, useNonDurableEntitiesOption, SharedOptions.ConnectionString, runUntilCancelled);

            return migrateCommand;
        }

        public Task Run(bool useDurableEntities, string connectionString, bool runUntilCancelled, CancellationToken cancellationToken = default)
        {
            CommandRunner.Run(connectionString, channel =>
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        for (int currentDelayLevel = NumberOfDelayLevelQueues; currentDelayLevel >= 0 && !cancellationToken.IsCancellationRequested; currentDelayLevel--)
                        {
                            MigrateQueue(channel, currentDelayLevel, useDurableEntities, cancellationToken);
                        }

                        if (!runUntilCancelled)
                        {
                            break;
                        }
                    }
                }
                catch (OperationInterruptedException ex)
                {
                    if (ex.ShutdownReason.ReplyCode == 404)
                    {
                        Console.WriteLine($"{ex.ShutdownReason.ReplyText}, run installers prior to running this tool.");
                    }
                    else
                    {
                        throw;
                    }
                }
            });

            return Task.CompletedTask;
        }

        void MigrateQueue(IModel channel, int currentDelayLevel, bool useDurableQueues, CancellationToken cancellationToken)
        {
            var currentDelayQueue = $"nsb.delay-level-{currentDelayLevel:00}";
            var messageCount = channel.MessageCount(currentDelayQueue);
            var declaredDestinationQueues = new HashSet<string>();

            if (messageCount > 0)
            {
                Console.Write($"Processing {messageCount} messages at delay level {currentDelayLevel:00}. ");

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

                    if (message.BasicProperties == null)
                    {
                        skippedMessages++;
                        channel.BasicNack(message.DeliveryTag, false, true);
                        continue;
                    }

                    int delayInSeconds = (int)message.BasicProperties.Headers[DelayInSecondsHeader];
                    DateTimeOffset timeSent = GetTimeSent(message);

                    (string destinationQueue, string newRoutingKey, int newDelayLevel) = GetNewRoutingKey(delayInSeconds, timeSent, message.RoutingKey, DateTimeOffset.UtcNow);

                    // Make sure the destination queue is bound to the delivery exchange to ensure delivery 
                    if (!declaredDestinationQueues.Contains(destinationQueue))
                    {
                        var arguments = new Dictionary<string, object>();

                        channel.ExchangeDeclare(destinationQueue, ExchangeType.Fanout, useDurableQueues, false, arguments);
                        channel.ExchangeBind(destinationQueue, "nsb.delay-delivery", $"#.{destinationQueue}");

                        declaredDestinationQueues.Add(destinationQueue);
                    }

                    string publishExchange = $"nsb.delay-level-{newDelayLevel:00}.2";

                    channel.BasicPublish(publishExchange, newRoutingKey, message.BasicProperties, message.Body);
                    channel.BasicAck(message.DeliveryTag, false);
                    processedMessages++;
                }

                Console.WriteLine($"{processedMessages} successful, {skippedMessages} skipped.");
            }
        }

        public (string DestinationQueue, string NewRoutingKey, int NewDelayLevel) GetNewRoutingKey(int delayInSeconds, DateTimeOffset timeSent, string currentRoutingKey, DateTimeOffset utcNow)
        {
            DateTimeOffset originalDeliveryDate = timeSent.AddSeconds(delayInSeconds);
            int newDelayInSeconds = Convert.ToInt32(originalDeliveryDate.Subtract(utcNow).TotalSeconds);
            string destinationQueue = currentRoutingKey.Substring(currentRoutingKey.LastIndexOf('.') + 1);
            string newRoutingKey = DelayInfrastructure.CalculateRoutingKey(newDelayInSeconds, destinationQueue, out int newDelayLevel);

            return (destinationQueue, newRoutingKey, newDelayLevel);
        }

        DateTimeOffset GetTimeSent(BasicGetResult message)
        {
            string? timeSentString = Encoding.UTF8.GetString((byte[])message.BasicProperties.Headers[TimeSentHeader]);
            return DateTimeOffset.ParseExact(timeSentString, DateTimeOffsetWireFormat, CultureInfo.InvariantCulture);
        }
    }
}
