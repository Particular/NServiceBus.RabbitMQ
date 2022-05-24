namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System;
    using System.CommandLine;
    using global::RabbitMQ.Client;
    class MigrateEndpointCommand
    {
        public static Command CreateCommand()
        {
            var quietModeOption = new Option<bool>(
             name: "--Quiet",
             description: $"Disable console output while running");

            var endpointOption = SharedOptions.CreateConnectionStringOption();


            quietModeOption.AddAlias("-q");

            var migrateCommand = new Command("migrate-to-quorum", "Migrate and existing endpoint to use quorum queues.");

            var endpointArgument = new Argument<string>();

            var connectionStringOption = SharedOptions.CreateConnectionStringOption();
            var topologyOption = SharedOptions.CreateRoutingTopologyOption();
            var useDurableEntitiesOption = SharedOptions.CreateUseDurableEntities();

            migrateCommand.AddArgument(endpointArgument);

            migrateCommand.AddOption(connectionStringOption);
            migrateCommand.AddOption(topologyOption);
            migrateCommand.AddOption(useDurableEntitiesOption);
            migrateCommand.AddOption(quietModeOption);

            migrateCommand.SetHandler(async (string endpoint, string connectionString, Topology routingTopology, bool useDurableEntities, bool quietMode, CancellationToken cancellationToken) =>
            {
                var migrationProcess = new MigrateEndpointCommand();
                await migrationProcess.Run(endpoint, connectionString, routingTopology, useDurableEntities, quietMode, cancellationToken).ConfigureAwait(false);
            }, endpointArgument, connectionStringOption, topologyOption, useDurableEntitiesOption, quietModeOption);

            return migrateCommand;
        }

        public Task Run(string queueName, string connectionString, Topology routingTopology, bool useDurableEntities, bool quietMode, CancellationToken cancellationToken = default)
        {
            if (!quietMode)
            {
                Console.WriteLine($"Starting migration of {queueName}");
            }

            if (routingTopology != Topology.Conventional)
            {
                throw new NotSupportedException("Quorum queue migration is only supported for the ConventionalRoutingTopology for the moment.");
            }

            CommandRunner.Run(connectionString, channel =>
            {
                try
                {
                    // make sure that the endpoint queue exists
                    channel.MessageCount(queueName);
                }
                catch (Exception)
                {
                    throw new Exception($"Input queue for endpoint {queueName} could not be found.");
                }

                //check if queue already is quorum
                // TODO: needs a new channel?
                //var isQuorum = true;
                //try
                //{
                //    channel.QueueDeclare(queueName, useDurableEntities, false, false, QuorumQueueArguments);
                //}
                //catch (Exception)
                //{
                //    isQuorum = false;
                //}

                //if (isQuorum)
                //{
                //    throw new Exception($"Queue {queueName} is already a quorum queue");
                //}

                var topology = new ConventionalRoutingTopology(useDurableEntities);
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
                    channel.BasicPublish(holdingQueueName, EmptyRoutingKey, message.BasicProperties, message.Body),
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
                    channel.BasicPublish(queueName, EmptyRoutingKey, message.BasicProperties, message.Body),
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

            for (var i = 0; i < messageCount && !cancellationToken.IsCancellationRequested; i++)
            {
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

        static string EmptyRoutingKey = string.Empty;
        static Dictionary<string, object> QuorumQueueArguments = new Dictionary<string, object> { { "x-queue-type", "quorum" } };
    }
}