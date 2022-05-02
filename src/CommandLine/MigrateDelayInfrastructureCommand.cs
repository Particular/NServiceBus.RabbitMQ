namespace NServiceBus.RabbitMQ.CommandLine
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
        const string ConnectionStringEnvironmentVariable = "RabbitMQTransport_ConnectionString";
        const string DelayInSecondsHeader = "NServiceBus.Transport.RabbitMQ.DelayInSeconds";
        const string TimeSentHeader = "NServiceBus.TimeSent";
        const string DateTimeOffsetWireFormat = "yyyy-MM-dd HH:mm:ss:ffffff Z";
        const int HighestDelayLevel = 0x4000000;

        public static Command CreateCommand()
        {
            var connectionStringOption = new Option<string>(
                    name: "--connectionString",
                    description: $"Overrides environment variable '{ConnectionStringEnvironmentVariable}'",
                    getDefaultValue: () => Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable) ?? string.Empty);

            connectionStringOption.AddAlias("-c");

            var migrateCommand = new Command("migrate-delay-infrastructure", "Migrate existing delay queues and in-flight delayed messages to the latest infrustructure.");
            migrateCommand.AddOption(connectionStringOption);

            migrateCommand.SetHandler(async (string connectionString, CancellationToken cancellationToken) =>
            {
                var migrationProcess = new MigrateDelayInfrastructureCommand();
                await migrationProcess.Execute(connectionString, cancellationToken).ConfigureAwait(false);

            }, connectionStringOption);

            return migrateCommand;
        }

        public Task Execute(string connectionString, CancellationToken cancellationToken = default)
        {
            Console.WriteLine("Migrating delay infrustructure...");

            var factory = new ConnectionFactory();
            factory.CreateConnection(connectionString);

            using (IConnection connection = factory.CreateConnection())
            {
                using (IModel channel = connection.CreateModel())
                {
                    try
                    {
                        var messageCount = channel.MessageCount("nsb.delay-level-27");

                        if (messageCount > 0)
                        {
                            Console.WriteLine($"Found {messageCount} messages at delay level 27");

                            for (int i = 0; i < messageCount; i++)
                            {
                                var message = channel.BasicGet("nsb.delay-level-27", false);

                                if (message == null)
                                {
                                    // We hit the end of the queue.
                                    break;
                                }

                                if (message.BasicProperties == null)
                                {
                                    Console.WriteLine("Message lacks headers..");
                                    break;
                                }

                                int delayInSeconds = (int)message.BasicProperties.Headers[DelayInSecondsHeader];
                                var timeSent = GetTimeSent(message);

                                var originalDeliveryDate = timeSent.AddSeconds(delayInSeconds);

                                var newDelayInSeconds = Convert.ToInt32(originalDeliveryDate.Subtract(DateTimeOffset.UtcNow).TotalSeconds);
                                var destinationQueue = message.RoutingKey.Substring(message.RoutingKey.LastIndexOf('.') + 1);

                                string newRoutingId = GetRoutingId(newDelayInSeconds, destinationQueue, out int deliveryDelayLevel);

                                channel.QueueBind(destinationQueue, $"nsb.delay-delivery", $"#.{destinationQueue}");
                                string publishExchange = $"nsb.delay-level-{deliveryDelayLevel:00}.2";

                                channel.BasicPublish(publishExchange, newRoutingId, message.BasicProperties, message.Body);
                                channel.BasicAck(message.DeliveryTag, false);
                            }
                        }
                    }
                    catch (OperationInterruptedException ex)
                    {
                        if (ex.ShutdownReason.ReplyCode == 404)
                        {
                            Console.WriteLine($"{ex.ShutdownReason.ReplyText}, run installers prior to running this tool.");
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

        DateTimeOffset GetTimeSent(BasicGetResult message)
        {
            string? timeSentString = Encoding.UTF8.GetString((byte[])message.BasicProperties.Headers[TimeSentHeader]);
            return DateTimeOffset.ParseExact(timeSentString, DateTimeOffsetWireFormat, CultureInfo.InvariantCulture);
        }

        string GetRoutingId(int delayInSeconds, string destinationQueue, out int deliveryDelayLevel)
        {
            var routingIdBuilder = new StringBuilder();
            deliveryDelayLevel = 0;

            for (int delayLevel = HighestDelayLevel; delayLevel > 0; delayLevel >>= 1)
            {
                int levelBit = delayLevel & delayInSeconds;
                routingIdBuilder.Append(levelBit == 0 ? "0." : "1.");

                if (levelBit != 0 && deliveryDelayLevel == 0)
                {
                    deliveryDelayLevel = Convert.ToInt32(Math.Log2(delayLevel) + 1);
                }
            }

            routingIdBuilder.Append(destinationQueue);

            return routingIdBuilder.ToString();
        }
    }
}
