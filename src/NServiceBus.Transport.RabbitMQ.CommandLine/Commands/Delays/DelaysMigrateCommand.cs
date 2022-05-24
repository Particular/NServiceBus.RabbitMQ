namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System;
    using System.CommandLine;
    using System.Globalization;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Exceptions;

    class DelaysMigrateCommand
    {
        const string timeSentHeader = "NServiceBus.TimeSent";
        const string dateTimeOffsetWireFormat = "yyyy-MM-dd HH:mm:ss:ffffff Z";

        public static Command CreateCommand()
        {
            var command = new Command("migrate", "Migrate existing delay queues and in-flight delayed messages to the latest infrustructure.");

            var connectionStringOption = SharedOptions.CreateConnectionStringOption();
            var topologyOption = SharedOptions.CreateRoutingTopologyOption();
            var useDurableEntitiesOption = SharedOptions.CreateUseDurableEntitiesOption();
            var certPathOption = SharedOptions.CreateCertPathOption();
            var certPassphraseOption = SharedOptions.CreateCertPassphraseOption();

            var quietModeOption = new Option<bool>(name: "--Quiet", description: $"Disable console output while running");
            quietModeOption.AddAlias("-q");

            var runUntilCancelled = new Option<bool>(name: "--RunUntilCancelled", description: $"The migration script will run until the script is cancelled");
            runUntilCancelled.AddAlias("-r");

            command.AddOption(connectionStringOption);
            command.AddOption(topologyOption);
            command.AddOption(useDurableEntitiesOption);
            command.AddOption(certPathOption);
            command.AddOption(certPassphraseOption);
            command.AddOption(quietModeOption);
            command.AddOption(runUntilCancelled);

            command.SetHandler(async (string connectionString, Topology topology, bool useDurableEntities, string certPath, string certPassphrase, bool quietMode, bool runUntilCancelled, CancellationToken cancellationToken) =>
            {
                var routingTopology = GetRoutingTopology(topology, useDurableEntities);

                X509Certificate2? certificate = null;

                if (!string.IsNullOrEmpty(certPath) && !string.IsNullOrWhiteSpace(certPassphrase))
                {
                    certificate = new X509Certificate2(certPath, certPassphrase);
                }

                var migrationProcess = new DelaysMigrateCommand(connectionString, routingTopology, certificate, quietMode, runUntilCancelled);

                await migrationProcess.Run(cancellationToken).ConfigureAwait(false);

            }, connectionStringOption, topologyOption, useDurableEntitiesOption, certPathOption, certPassphraseOption, quietModeOption, runUntilCancelled);

            return command;
        }

        static IRoutingTopology GetRoutingTopology(Topology routingTopology, bool useDurableEntities) => routingTopology switch
        {
            Topology.Conventional => new ConventionalRoutingTopology(useDurableEntities),
            Topology.Direct => new DirectRoutingTopology(useDurableEntities),
            _ => throw new InvalidOperationException()
        };

        static (string DestinationQueue, string NewRoutingKey, int NewDelayLevel) GetNewRoutingKey(int delayInSeconds, DateTimeOffset timeSent, string currentRoutingKey, DateTimeOffset utcNow)
        {
            DateTimeOffset originalDeliveryDate = timeSent.AddSeconds(delayInSeconds);
            int newDelayInSeconds = Convert.ToInt32(originalDeliveryDate.Subtract(utcNow).TotalSeconds);
            string destinationQueue = currentRoutingKey.Substring(currentRoutingKey.LastIndexOf('.') + 1);
            string newRoutingKey = DelayInfrastructure.CalculateRoutingKey(newDelayInSeconds, destinationQueue, out int newDelayLevel);

            return (destinationQueue, newRoutingKey, newDelayLevel);
        }
        static DateTimeOffset GetTimeSent(BasicGetResult message)
        {
            string? timeSentString = Encoding.UTF8.GetString((byte[])message.BasicProperties.Headers[timeSentHeader]);
            return DateTimeOffset.ParseExact(timeSentString, dateTimeOffsetWireFormat, CultureInfo.InvariantCulture);
        }

        public DelaysMigrateCommand(string connectionString, IRoutingTopology routingTopology, X509Certificate2? certificate, bool quietMode, bool runUntilCancelled)
        {
            this.connectionString = connectionString;
            this.routingTopology = routingTopology;
            this.certificate = certificate;
            this.quietMode = quietMode;
            this.runUntilCancelled = runUntilCancelled;
        }

        public Task Run(CancellationToken cancellationToken = default)
        {
            CommandRunner.Run(connectionString, certificate, channel =>
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        for (int currentDelayLevel = DelayInfrastructure.MaxLevel; currentDelayLevel >= 0 && !cancellationToken.IsCancellationRequested; currentDelayLevel--)
                        {
                            MigrateQueue(channel, currentDelayLevel, cancellationToken);
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

        void MigrateQueue(IModel channel, int currentDelayLevel, CancellationToken cancellationToken)
        {
            var currentDelayQueue = $"nsb.delay-level-{currentDelayLevel:00}";
            var messageCount = channel.MessageCount(currentDelayQueue);
            var declaredDestinationQueues = new HashSet<string>();

            if (messageCount > 0)
            {
                if (!quietMode)
                {
                    Console.Write($"Processing {messageCount} messages at delay level {currentDelayLevel:00}. ");
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

                    if (message.BasicProperties == null)
                    {
                        skippedMessages++;
                        channel.BasicNack(message.DeliveryTag, false, true);
                        continue;
                    }

                    int delayInSeconds = (int)message.BasicProperties.Headers[DelayInfrastructure.DelayHeader];
                    DateTimeOffset timeSent = GetTimeSent(message);

                    (string destinationQueue, string newRoutingKey, int newDelayLevel) = GetNewRoutingKey(delayInSeconds, timeSent, message.RoutingKey, DateTimeOffset.UtcNow);

                    // Make sure the destination queue is bound to the delivery exchange to ensure delivery
                    if (!declaredDestinationQueues.Contains(destinationQueue))
                    {
                        routingTopology.BindToDelayInfrastructure(channel, destinationQueue, DelayInfrastructure.DeliveryExchange, DelayInfrastructure.BindingKey(destinationQueue));
                        declaredDestinationQueues.Add(destinationQueue);
                    }

                    string publishExchange = DelayInfrastructure.LevelName(newDelayLevel);

                    channel.BasicPublish(publishExchange, newRoutingKey, message.BasicProperties, message.Body);
                    channel.BasicAck(message.DeliveryTag, false);
                    processedMessages++;
                }

                if (!quietMode)
                {
                    Console.WriteLine($"{processedMessages} successful, {skippedMessages} skipped.");
                }
            }
        }

        readonly string connectionString;
        readonly IRoutingTopology routingTopology;
        readonly X509Certificate2? certificate;
        readonly bool quietMode;
        readonly bool runUntilCancelled;
    }
}
