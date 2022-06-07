namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System;
    using System.CommandLine;
    using global::RabbitMQ.Client;

    class MigrateEndpointCommand
    {
        public static Command CreateCommand()
        {
            var endpointOption = SharedOptions.CreateConnectionStringOption();

            var command = new Command("migrate-to-quorum", "Migrate and existing endpoint to use quorum queues.");

            var endpointArgument = new Argument<string>();

            var connectionFactoryBinder = SharedOptions.CreateConnectionFactoryBinderWithOptions(command);

            var topologyOption = SharedOptions.CreateRoutingTopologyOption();
            var useDurableEntitiesOption = SharedOptions.CreateUseDurableEntitiesOption();

            command.AddArgument(endpointArgument);

            command.AddOption(topologyOption);
            command.AddOption(useDurableEntitiesOption);

            command.SetHandler(async (string endpoint, RabbitMQ.ConnectionFactory connectionFactory, Topology routingTopology, bool useDurableEntities, IConsole console, CancellationToken cancellationToken) =>
            {
                var migrateCommand = new MigrateEndpointCommand(endpoint, connectionFactory, routingTopology, useDurableEntities, console);
                await migrateCommand.Run(cancellationToken).ConfigureAwait(false);
            }, endpointArgument, connectionFactoryBinder, topologyOption, useDurableEntitiesOption);

            return command;
        }

        public MigrateEndpointCommand(
            string queueName,
            RabbitMQ.ConnectionFactory connectionFactory,
            Topology routingTopology,
            bool useDurableEntities,
            IConsole console)
        {
            this.queueName = queueName;
            this.connectionFactory = connectionFactory;
            this.routingTopology = routingTopology;
            this.useDurableEntities = useDurableEntities;
            this.console = console;
            holdingQueueName = $"{queueName}-migration-temp";
        }

        public Task Run(CancellationToken cancellationToken = default)
        {
            console.WriteLine($"Starting migration of {queueName}");

            if (routingTopology != Topology.Conventional)
            {
                throw new NotSupportedException("Quorum queue migration is only supported for the ConventionalRoutingTopology for the moment.");
            }

            using (var connection = connectionFactory.CreateAdministrationConnection())
            {
                using (var channel = connection.CreateModel())
                {
                    Validate(channel, cancellationToken);
                }

                using (var channel = connection.CreateModel())
                {
                    ConvertToQuorum(channel, cancellationToken);
                }

                // Due to https://github.com/dotnet/command-line-api/issues/1750 we need to use a separate channel to move
                // the messages back to the main queue again.
                using (var channel = connection.CreateModel())
                {
                    RestoreMessages(channel, cancellationToken);
                }
            }

            return Task.CompletedTask;
        }
        void Validate(IModel channel, CancellationToken cancellationToken)
        {
            // make sure that the endpoint queue exists
            channel.MessageCount(queueName);

            // check if queue already is quorum
            try
            {
                channel.QueueDeclare(queueName, useDurableEntities, false, false, QuorumQueueArguments);
            }
            catch (Exception)
            {
                return;
            }

            throw new Exception($"Queue {queueName} is already a quorum queue");
        }

        void ConvertToQuorum(IModel channel, CancellationToken cancellationToken)
        {
            // does the holding queue need to be quorum?
            channel.QueueDeclare(holdingQueueName, true, false, false, QuorumQueueArguments);
            console.WriteLine($"Holding queue created: {holdingQueueName}");

            // bind the holding queue to the default exchange of queue under migration
            // this will throw if the exchange for the endpoint doesn't exist
            channel.QueueBind(holdingQueueName, queueName, EmptyRoutingKey);

            console.WriteLine($"Holding queue bound to main queue exchange");

            // unbind the queue under migration to stopp more messages from coming in
            channel.QueueUnbind(queueName, queueName, EmptyRoutingKey);
            console.WriteLine($"Main queue unbind");

            // move all existing messages to the holding queue
            channel.ConfirmSelect();

            var numMessagesMovedToHolding = ProcessMessages(
                channel,
                queueName,
                message =>
                {
                    channel.BasicPublish(EmptyRoutingKey, holdingQueueName, message.BasicProperties, message.Body);
                    channel.WaitForConfirmsOrDie();
                },
                cancellationToken);

            console.WriteLine($"{numMessagesMovedToHolding} messages moved to the holding queue");

            // delete the queue under migration
            channel.QueueDelete(queueName);
            console.WriteLine($"Main queue removed");

            // recreate the queue
            channel.QueueDeclare(queueName, useDurableEntities, false, false, QuorumQueueArguments);
            console.WriteLine($"Main queue recreated as a quorum queue");

            channel.QueueBind(queueName, queueName, EmptyRoutingKey);
            console.WriteLine($"Main queue binding to its exchange re-added");

            channel.QueueUnbind(holdingQueueName, queueName, EmptyRoutingKey);
            console.WriteLine($"Holding queue unbinded from main queue exchange");
        }
        void RestoreMessages(IModel channel, CancellationToken cancellationToken)
        {
            var messageIds = new Dictionary<string, string>();

            // move all messages in the holding queue back to the main queue
            channel.ConfirmSelect();

            var numMessageMovedBackToMain = ProcessMessages(
                channel,
                holdingQueueName,
                message =>
                {
                    string? messageIdString = null;

                    if (message.BasicProperties.Headers.TryGetValue("NServiceBus.MessageId", out var messageId))
                    {
                        messageIdString = messageId?.ToString();

                        if (messageIdString != null && messageIds.ContainsKey(messageIdString))
                        {
                            return;
                        }
                    }

                    channel.BasicPublish(EmptyRoutingKey, queueName, message.BasicProperties, message.Body);
                    channel.WaitForConfirmsOrDie();

                    if (messageIdString != null)
                    {
                        messageIds.Add(messageIdString, string.Empty);
                    }
                },
                cancellationToken);

            console.WriteLine($"{numMessageMovedBackToMain} messages moved back to main queue");


            channel.QueueDelete(holdingQueueName);
            console.WriteLine($"Holding queue removed");
        }
        uint ProcessMessages(
            IModel channel,
            string sourceQueue,
            Action<BasicGetResult> onMoveMessage,
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

                onMoveMessage(message);

                channel.BasicAck(message.DeliveryTag, false);
            }

            return messageCount;
        }

        readonly string queueName;
        readonly string holdingQueueName;
        readonly RabbitMQ.ConnectionFactory connectionFactory;
        readonly Topology routingTopology;
        readonly bool useDurableEntities;
        readonly IConsole console;
        static string EmptyRoutingKey = string.Empty;
        static Dictionary<string, object> QuorumQueueArguments = new Dictionary<string, object> { { "x-queue-type", "quorum" } };
    }
}