namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System;
    using System.CommandLine;
    using global::RabbitMQ.Client;

    public class MigrateEndpointCommand
    {
        public static Command CreateCommand()
        {
            var endpointOption = SharedOptions.CreateConnectionStringOption();

            var migrateCommand = new Command("migrate-to-quorum", "Migrate and existing endpoint to use quorum queues.");

            var endpointArgument = new Argument<string>();

            var connectionStringOption = SharedOptions.CreateConnectionStringOption();
            var topologyOption = SharedOptions.CreateRoutingTopologyOption();
            var useDurableEntitiesOption = SharedOptions.CreateUseDurableEntities();

            migrateCommand.AddArgument(endpointArgument);

            migrateCommand.AddOption(connectionStringOption);
            migrateCommand.AddOption(topologyOption);
            migrateCommand.AddOption(useDurableEntitiesOption);

            migrateCommand.SetHandler(async (string endpoint, string connectionString, Topology routingTopology, bool useDurableEntities, CancellationToken cancellationToken) =>
            {
                var migrationProcess = new MigrateEndpointCommand();
                await migrationProcess.Run(endpoint, connectionString, routingTopology, useDurableEntities, cancellationToken).ConfigureAwait(false);
            }, endpointArgument, connectionStringOption, topologyOption, useDurableEntitiesOption);

            return migrateCommand;
        }

        public Task Run(string queueName, string connectionString, Topology routingTopology, bool useDurableEntities, CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"Starting migration of {queueName}");

            if (routingTopology != Topology.Conventional)
            {
                throw new NotSupportedException("Quorum queue migration is only supported for the ConventionalRoutingTopology for the moment.");
            }

            CommandRunner.Run(connectionString, (connection, channel) =>
            {
                // make sure that the endpoint queue exists
                channel.MessageCount(queueName);

                //check if queue already is quorum
                if (SafeExecute(connection, ch => ch.QueueDeclare(queueName, useDurableEntities, false, false, QuorumQueueArguments)))
                {
                    throw new Exception($"Queue {queueName} is already a quorum queue");
                }

                var holdingQueueName = $"{queueName}-migration-temp";

                //does the holding queue need to be quorum?
                channel.QueueDeclare(holdingQueueName, true, false, false, QuorumQueueArguments);
                Console.WriteLine($"Holding queue created: {holdingQueueName}");

                //bind the holding queue to the default exchange of queue under migration
                // this will throw if the exchange for the endpoint doesn't exist
                channel.QueueBind(holdingQueueName, queueName, EmptyRoutingKey);

                Console.WriteLine($"Holding queue bound to main queue exchange");

                //unbind the queue under migration to stopp more messages from coming in
                channel.QueueUnbind(queueName, queueName, EmptyRoutingKey);
                Console.WriteLine($"Main queue unbind");

                //move all existing messages to the holding queue
                var numMessagesMovedToHolding = ProcessMessages(
                    channel,
                    queueName,
                    (message, channel) =>
                    channel.BasicPublish(EmptyRoutingKey, holdingQueueName, message.BasicProperties, message.Body),
                    cancellationToken);

                Console.WriteLine($"{numMessagesMovedToHolding} messages moved to the holding queue");

                // delete the queue under migration
                channel.QueueDelete(queueName);
                Console.WriteLine($"Main queue removed");

                //recreate the queue
                channel.QueueDeclare(queueName, useDurableEntities, false, false, QuorumQueueArguments);
                Console.WriteLine($"Main queue recreated as a quorum queue");

                channel.QueueBind(queueName, queueName, EmptyRoutingKey);
                Console.WriteLine($"Main queue binding to its exchange re-added");

                channel.QueueUnbind(holdingQueueName, queueName, EmptyRoutingKey);

                Console.WriteLine($"Holding queue unbinded from main queue exchange");

                //move all messages in the holding queue back to the main queue
                var numMessageMovedBackToMain = ProcessMessages(
                    channel,
                    holdingQueueName,
                    (message, channel) =>
                    //todo: deduplicate?
                    channel.BasicPublish(EmptyRoutingKey, queueName, message.BasicProperties, message.Body),
                    cancellationToken);

                Console.WriteLine($"{numMessageMovedBackToMain} messages moved back to main queue");

                channel.QueueDelete(holdingQueueName);
                Console.WriteLine($"Holding queue removed");
            });

            return Task.CompletedTask;
        }

        uint ProcessMessages(
            IModel channel,
            string sourceQueue,
            Action<BasicGetResult, IModel> onMoveMessage,
            CancellationToken cancellationToken)
        {
            var messageCount = channel.MessageCount(sourceQueue);

            for (var i = 0; i < messageCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var message = channel.BasicGet(sourceQueue, false);

                if (message == null)
                {
                    // Queue is empty
                    break;
                }

                //what is the scenario for this?
                if (message.BasicProperties == null)
                {
                    channel.BasicNack(message.DeliveryTag, false, true);
                    continue;
                }

                onMoveMessage(message, channel);

                channel.BasicAck(message.DeliveryTag, false);
            }

            return messageCount;
        }

        bool SafeExecute(IConnection connection, Action<IModel> command)
        {
            try
            {
                // create temporary channel as the channel will be faulted if the queue does not exist.
                using (var tempChannel = connection.CreateModel())
                {
                    command(tempChannel);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        static string EmptyRoutingKey = string.Empty;
        static Dictionary<string, object> QuorumQueueArguments = new Dictionary<string, object> { { "x-queue-type", "quorum" } };
    }
}