namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System;
    using System.CommandLine;
    using System.Globalization;
    using System.Text;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Exceptions;
    using NServiceBus.Transport.RabbitMQ.CommandLine.Configuration;

    public class MigrateDelayInfrastructureCommand
    {
        const string ConnectionStringEnvironmentVariable = "RabbitMQTransport_ConnectionString";
        const string DelayInSecondsHeader = "NServiceBus.Transport.RabbitMQ.DelayInSeconds";
        const string TimeSentHeader = "NServiceBus.TimeSent";
        const string DateTimeOffsetWireFormat = "yyyy-MM-dd HH:mm:ss:ffffff Z";
        const int NumberOfDelayLevelQueues = 27;

        public static Command CreateCommand()
        {
            var connectionStringOption = new Option<string>(
                    name: "--connectionString",
                    description: $"Overrides environment variable '{ConnectionStringEnvironmentVariable}'",
                    getDefaultValue: () => Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable) ?? string.Empty);

            connectionStringOption.AddAlias("-c");

            var useNonDurableEntitiesOption = new Option<bool>(
                 name: "--UseNonDurableEntities",
                 description: $"Create non-durable endpoint queues and exchanges");

            useNonDurableEntitiesOption.AddAlias("-n");

            var migrateCommand = new Command("migrate-delay-infrastructure", "Migrate existing delay queues and in-flight delayed messages to the latest infrustructure.");
            migrateCommand.AddOption(connectionStringOption);
            migrateCommand.AddOption(useNonDurableEntitiesOption);

            migrateCommand.SetHandler(async (string endpointQueueType, bool useNonDurableEntities, string connectionString, CancellationToken cancellationToken) =>
            {
                var migrationProcess = new MigrateDelayInfrastructureCommand();
                await migrationProcess.Execute(endpointQueueType, !useNonDurableEntities, connectionString, cancellationToken).ConfigureAwait(false);

            }, useNonDurableEntitiesOption, connectionStringOption);

            return migrateCommand;
        }

        public Task Execute(string endpointQueueType, bool useDurableEntities, string connectionString, CancellationToken cancellationToken = default)
        {
            Console.WriteLine("Migrating delay infrustructure...");

            var connectionData = ConnectionSettings.Parse(connectionString);

            var factory = new ConnectionFactory
            {
                HostName = connectionData.Host,
                Port = connectionData.Port,
                VirtualHost = connectionData.VHost,
                UserName = connectionData.UserName,
                Password = connectionData.Password,
                RequestedHeartbeat = connectionData.HeartbeatInterval,
                NetworkRecoveryInterval = connectionData.NetworkRecoveryInterval,
                UseBackgroundThreadsForIO = true,
                DispatchConsumersAsync = true
            };

            using (IConnection connection = factory.CreateConnection())
            {
                using (IModel channel = connection.CreateModel())
                {
                    try
                    {
                        for (int currentDelayLevel = NumberOfDelayLevelQueues; currentDelayLevel >= 0; currentDelayLevel--)
                        {
                            MigrateQueue(channel, currentDelayLevel, useDurableEntities);
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
                    finally
                    {
                        channel.Close();
                    }
                }

                connection.Close();
            }

            return Task.CompletedTask;
        }

        void MigrateQueue(IModel channel, int currentDelayLevel, bool useDurableQueues)
        {
            var currentDelayQueue = $"nsb.delay-level-{currentDelayLevel:00}";
            var messageCount = channel.MessageCount(currentDelayQueue);
            var createdDestinationQueues = new HashSet<string>();

            if (messageCount > 0)
            {
                Console.Write($"Processing {messageCount} messages at delay level {currentDelayLevel:00}. ");
                int skippedMessages = 0;
                int processedMessages = 0;

                for (int i = 0; i < messageCount; i++)
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
                    DateTimeOffset originalDeliveryDate = timeSent.AddSeconds(delayInSeconds);
                    int newDelayInSeconds = Convert.ToInt32(originalDeliveryDate.Subtract(DateTimeOffset.UtcNow).TotalSeconds);
                    string destinationQueue = message.RoutingKey.Substring(message.RoutingKey.LastIndexOf('.') + 1);
                    string newRoutingId = DelayInfrastructure.CalculateRoutingKey(newDelayInSeconds, destinationQueue, out int deliveryDelayLevel);

                    if (!createdDestinationQueues.Contains(destinationQueue))
                    {
                        var arguments = new Dictionary<string, object>();

                        channel.ExchangeDeclare(destinationQueue, ExchangeType.Fanout, useDurableQueues, false, arguments);
                        channel.ExchangeBind(destinationQueue, "nsb.delay-delivery", $"#.{destinationQueue}");

                        createdDestinationQueues.Add(destinationQueue);
                    }

                    string publishExchange = $"nsb.delay-level-{deliveryDelayLevel:00}.2";

                    channel.BasicPublish(publishExchange, newRoutingId, message.BasicProperties, message.Body);
                    channel.BasicAck(message.DeliveryTag, false);
                    processedMessages++;
                }

                Console.WriteLine($"{processedMessages} successful, {skippedMessages} skipped.");
            }
        }

        DateTimeOffset GetTimeSent(BasicGetResult message)
        {
            string? timeSentString = Encoding.UTF8.GetString((byte[])message.BasicProperties.Headers[TimeSentHeader]);
            return DateTimeOffset.ParseExact(timeSentString, DateTimeOffsetWireFormat, CultureInfo.InvariantCulture);
        }
    }
}
