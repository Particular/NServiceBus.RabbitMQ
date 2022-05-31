namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System;
    using System.CommandLine;
    using System.Globalization;
    using System.Text;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Exceptions;

    class DelaysMigrateCommand
    {
        const string poisonMessageQueue = "delays-migrate-poison-messages";
        const string timeSentHeader = "NServiceBus.TimeSent";
        const string dateTimeOffsetWireFormat = "yyyy-MM-dd HH:mm:ss:ffffff Z";

        public static Command CreateCommand()
        {
            var command = new Command("migrate", "Migrate in-flight delayed messages to the v2 delay infrustructure.");

            var topologyOption = SharedOptions.CreateRoutingTopologyOption();
            var useDurableEntitiesOption = SharedOptions.CreateUseDurableEntitiesOption();
            var connectionFactoryBinder = SharedOptions.CreateConnectionFactoryBinderWithOptions(command);

            var quietModeOption = new Option<bool>(name: "--Quiet", description: $"Disable console output while running");
            quietModeOption.AddAlias("-q");

            command.AddOption(topologyOption);
            command.AddOption(useDurableEntitiesOption);
            command.AddOption(quietModeOption);

            command.SetHandler(async (RabbitMQ.ConnectionFactory connectionFactory, Topology topology, bool useDurableEntities, bool quietMode, IConsole console, CancellationToken cancellationToken) =>
            {
                var routingTopology = GetRoutingTopology(topology, useDurableEntities);

                var delaysMigrate = new DelaysMigrateCommand(connectionFactory, routingTopology, quietMode, console);

                await delaysMigrate.Run(cancellationToken).ConfigureAwait(false);

            }, connectionFactoryBinder, topologyOption, useDurableEntitiesOption, quietModeOption);

            return command;
        }

        public static (string DestinationQueue, string NewRoutingKey, int NewDelayLevel) GetNewRoutingKey(int delayInSeconds, DateTimeOffset timeSent, string currentRoutingKey, DateTimeOffset utcNow)
        {
            var originalDeliveryDate = timeSent.AddSeconds(delayInSeconds);
            var newDelayInSeconds = Convert.ToInt32(originalDeliveryDate.Subtract(utcNow).TotalSeconds);
            var destinationQueue = currentRoutingKey.Substring(currentRoutingKey.LastIndexOf('.') + 1);
            var newRoutingKey = DelayInfrastructure.CalculateRoutingKey(newDelayInSeconds, destinationQueue, out int newDelayLevel);

            return (destinationQueue, newRoutingKey, newDelayLevel);
        }

        static IRoutingTopology GetRoutingTopology(Topology routingTopology, bool useDurableEntities) => routingTopology switch
        {
            Topology.Conventional => new ConventionalRoutingTopology(useDurableEntities),
            Topology.Direct => new DirectRoutingTopology(useDurableEntities),
            _ => throw new InvalidOperationException()
        };

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

        public DelaysMigrateCommand(RabbitMQ.ConnectionFactory connectionFactory, IRoutingTopology routingTopology, bool quietMode, IConsole console)
        {
            this.connectionFactory = connectionFactory;
            this.routingTopology = routingTopology;
            this.quietMode = quietMode;
            this.console = console;
        }

        public Task Run(CancellationToken cancellationToken = default)
        {
            using var connection = connectionFactory.CreateAdministrationConnection();
            using var channel = connection.CreateModel();

            try
            {
                for (int currentDelayLevel = DelayInfrastructure.MaxLevel; currentDelayLevel >= 0 && !cancellationToken.IsCancellationRequested; currentDelayLevel--)
                {
                    MigrateQueue(channel, currentDelayLevel, cancellationToken);
                }
            }
            catch (OperationInterruptedException ex)
            {
                if (ex.ShutdownReason.ReplyCode == 404)
                {
                    console.WriteLine($"Fail: {ex.ShutdownReason.ReplyText}, run installers prior to running this tool.");
                }
                else
                {
                    console.WriteLine($"Fail: {ex.Message}");
                }
            }
            finally
            {
                if (channel.IsOpen)
                {
                    channel.Close();
                }

                if (connection.IsOpen)
                {
                    connection.Close();
                }
            }

            return Task.CompletedTask;
        }

        void MigrateQueue(IModel channel, int currentDelayLevel, CancellationToken cancellationToken)
        {
            var currentDelayQueue = $"nsb.delay-level-{currentDelayLevel:00}";
            var messageCount = channel.MessageCount(currentDelayQueue);
            var declaredDestinationQueues = new HashSet<string>();

            if (messageCount > 0)
            {
                if (!quietMode)
                {
                    console.Write($"Processing {messageCount} messages at delay level {currentDelayLevel:00}. ");
                }

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
                        channel.BasicAck(message.DeliveryTag, false);

                        continue;
                    }

                    var messageHeaders = message.BasicProperties.Headers;
                    var delayInSeconds = (int)messageHeaders[DelayInfrastructure.DelayHeader];
                    var timeSent = GetTimeSent(message);

                    (string destinationQueue, string newRoutingKey, int newDelayLevel) = GetNewRoutingKey(delayInSeconds, timeSent, message.RoutingKey, DateTimeOffset.UtcNow);

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
                    channel.BasicAck(message.DeliveryTag, false);
                    processedMessages++;
                }

                if (!quietMode)
                {
                    console.WriteLine($"{processedMessages} successful, {skippedMessages} skipped.");
                }
            }
            else
            {
                if (!quietMode)
                {
                    console.WriteLine($"No messages to process at delay level {currentDelayLevel:00}.");
                }
            }
        }

        readonly RabbitMQ.ConnectionFactory connectionFactory;
        readonly IRoutingTopology routingTopology;
        readonly bool quietMode;
        readonly IConsole console;
        bool poisonQueueCreated = false;
    }
}
