namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System;
    using System.CommandLine;
    using System.Text;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Exceptions;

    class QueueMigrateCommand
    {
        public static Command CreateCommand()
        {
            var command = new Command("migrate-to-quorum", "Migrate an existing classic queue to a quorum queue");

            var queueNameArgument = new Argument<string>()
            {
                Name = "queueName",
                Description = "The name of the classic queue to migrate to a quorum queue"
            };

            var brokerConnectionBinder = SharedOptions.CreateBrokerConnectionBinderWithOptions(command);

            command.AddArgument(queueNameArgument);

            command.SetHandler(async (queueName, brokerConnection, console, cancellationToken) =>
            {
                var migrateCommand = new QueueMigrateCommand(queueName, brokerConnection, console);
                await migrateCommand.Run(cancellationToken).ConfigureAwait(false);
            },
            queueNameArgument, brokerConnectionBinder, Bind.FromServiceProvider<IConsole>(), Bind.FromServiceProvider<CancellationToken>());

            return command;
        }

        public QueueMigrateCommand(string queueName, BrokerConnection brokerConnection, IConsole console)
        {
            this.queueName = queueName;
            this.brokerConnection = brokerConnection;
            this.console = console;
            holdingQueueName = $"{queueName}-migration-temp";
            migrationState = new MigrationState();
        }

        public Task Run(CancellationToken cancellationToken = default)
        {
            console.WriteLine($"Starting migration of '{queueName}'");

            using var connection = brokerConnection.Create();

            migrationState.SetInitialMigrationStage(queueName, holdingQueueName, connection);

            while (migrationState.CurrentStage != MigrationStage.CleanUpCompleted)
            {
                switch (migrationState.CurrentStage)
                {
                    case MigrationStage.Starting:
                        migrationState.CurrentStage = MoveMessagesToHoldingQueue(connection, cancellationToken);
                        break;
                    case MigrationStage.MessagesMovedToHoldingQueue:
                        migrationState.CurrentStage = DeleteMainQueue(connection);
                        break;
                    case MigrationStage.ClassicQueueDeleted:
                        migrationState.CurrentStage = CreateMainQueueAsQuorum(connection);
                        break;
                    case MigrationStage.QuorumQueueCreated:
                        migrationState.CurrentStage = RestoreMessages(connection, cancellationToken);
                        break;
                    case MigrationStage.MessagesMovedToQuorumQueue:
                        migrationState.CurrentStage = CleanUpHoldingQueue(connection);
                        break;
                    case MigrationStage.CleanUpCompleted:
                    default:
                        break;
                }
            }

            return Task.CompletedTask;
        }

        MigrationStage MoveMessagesToHoldingQueue(IConnection connection, CancellationToken cancellationToken)
        {
            using var channel = connection.CreateModel();

            console.WriteLine($"Migrating messages from '{queueName}' to '{holdingQueueName}'");

            // does the holding queue need to be quorum?
            channel.QueueDeclare(holdingQueueName, true, false, false, quorumQueueArguments);
            console.WriteLine($"Created queue '{holdingQueueName}'");

            // bind the holding queue to the exchange of the queue under migration
            // this will throw if the exchange for the queue doesn't exist
            channel.QueueBind(holdingQueueName, queueName, string.Empty);
            console.WriteLine($"Bound '{holdingQueueName}' to exchange '{queueName}'");

            // unbind the queue under migration to stop more messages from coming in
            channel.QueueUnbind(queueName, queueName, string.Empty);
            console.WriteLine($"Unbound '{queueName}' from exchange '{queueName}' ");

            // move all existing messages to the holding queue
            channel.ConfirmSelect();

            var numMessagesMovedToHolding = ProcessMessages(
                channel,
                queueName,
                message =>
                {
                    channel.BasicPublish(string.Empty, holdingQueueName, message.BasicProperties, message.Body);
                    channel.WaitForConfirmsOrDie();
                },
                cancellationToken);

            console.WriteLine($"Moved {numMessagesMovedToHolding} messages to '{holdingQueueName}'");

            return MigrationStage.MessagesMovedToHoldingQueue;
        }

        MigrationStage DeleteMainQueue(IConnection connection)
        {
            using var channel = connection.CreateModel();

            if (channel.MessageCount(queueName) > 0)
            {
                throw new Exception($"Queue '{queueName}' is not empty after message processing. This can occur if messages are being published directly to the queue during the migration process. Run the command again to retry message processing.");
            }

            // delete the queue under migration
            channel.QueueDelete(queueName);
            console.WriteLine($"Removed '{queueName}'");

            return MigrationStage.ClassicQueueDeleted;
        }

        MigrationStage CreateMainQueueAsQuorum(IConnection connection)
        {
            using var channel = connection.CreateModel();

            channel.QueueDeclare(queueName, true, false, false, quorumQueueArguments);
            console.WriteLine($"Recreated '{queueName}' as a quorum queue");

            return MigrationStage.QuorumQueueCreated;
        }

        MigrationStage RestoreMessages(IConnection connection, CancellationToken cancellationToken)
        {
            using var channel = connection.CreateModel();

            channel.QueueBind(queueName, queueName, string.Empty);
            console.WriteLine($"Re-bound '{queueName}' to exchange '{queueName}'");

            channel.QueueUnbind(holdingQueueName, queueName, string.Empty);
            console.WriteLine($"Unbound '{holdingQueueName}' from exchange '{queueName}'");

            var messageIds = new Dictionary<string, string>();

            // move all messages in the holding queue back to the main queue
            channel.ConfirmSelect();

            var numMessageMovedBackToMain = ProcessMessages(
                channel,
                holdingQueueName,
                message =>
                {
                    string messageIdString = null;

                    if (message.BasicProperties.Headers != null && message.BasicProperties.Headers.TryGetValue("NServiceBus.MessageId", out var messageId))
                    {
                        if (messageId is byte[] bytes)
                        {
                            messageIdString = Encoding.UTF8.GetString(bytes);
                        }

                        if (messageIdString != null && messageIds.ContainsKey(messageIdString))
                        {
                            return;
                        }
                    }

                    channel.BasicPublish(string.Empty, queueName, message.BasicProperties, message.Body);
                    channel.WaitForConfirmsOrDie();

                    if (messageIdString != null)
                    {
                        messageIds.Add(messageIdString, string.Empty);
                    }
                },
                cancellationToken);

            console.WriteLine($"Moved {numMessageMovedBackToMain} messages from '{holdingQueueName}' to '{queueName}'");

            return MigrationStage.MessagesMovedToQuorumQueue;
        }

        MigrationStage CleanUpHoldingQueue(IConnection connection)
        {
            using var channel = connection.CreateModel();

            if (channel.MessageCount(holdingQueueName) != 0)
            {
                throw new Exception($"'{holdingQueueName}' is not empty and was not deleted. Run the command again to retry message processing.");
            }

            channel.QueueDelete(holdingQueueName);
            console.WriteLine($"Removed '{holdingQueueName}'");

            return MigrationStage.CleanUpCompleted;
        }

        uint ProcessMessages(IModel channel, string sourceQueue, Action<BasicGetResult> onMoveMessage, CancellationToken cancellationToken)
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
        readonly BrokerConnection brokerConnection;
        readonly IConsole console;
        readonly MigrationState migrationState;

        static Dictionary<string, object> quorumQueueArguments = new Dictionary<string, object> { { "x-queue-type", "quorum" } };

        enum MigrationStage
        {
            Starting,
            MessagesMovedToHoldingQueue,
            ClassicQueueDeleted,
            QuorumQueueCreated,
            MessagesMovedToQuorumQueue,
            CleanUpCompleted
        }

        class MigrationState
        {
            bool mainQueueExists = false;
            bool mainQueueHasMessages = false;
            bool mainQueueIsQuorum = false;
            bool holdingQueueExists = false;
            bool holdingQueueHasMessages = false;

            public MigrationStage CurrentStage { get; set; }

            public void SetInitialMigrationStage(string queueName, string holdingQueueName, IConnection connection)
            {
                SetBrokerState(queueName, holdingQueueName, connection);

                if (mainQueueIsQuorum)
                {
                    if (holdingQueueHasMessages)
                    {
                        CurrentStage = MigrationStage.QuorumQueueCreated;
                    }
                    else if (holdingQueueExists)
                    {
                        CurrentStage = MigrationStage.MessagesMovedToQuorumQueue;
                    }
                    else
                    {
                        CurrentStage = MigrationStage.CleanUpCompleted;
                    }
                }
                else
                {
                    if (mainQueueHasMessages)
                    {
                        CurrentStage = MigrationStage.Starting;
                    }
                    else if (holdingQueueHasMessages && mainQueueExists)
                    {
                        CurrentStage = MigrationStage.MessagesMovedToHoldingQueue;
                    }
                    else if (holdingQueueHasMessages)
                    {
                        CurrentStage = MigrationStage.ClassicQueueDeleted;
                    }
                    else
                    {
                        if (mainQueueExists)
                        {
                            CurrentStage = MigrationStage.Starting;
                        }
                        else
                        {
                            throw new Exception($"Queue '{queueName}' was not found");
                        }
                    }
                }
            }

            void SetBrokerState(string queueName, string holdingQueueName, IConnection connection)
            {
                var channel = connection.CreateModel();

                try
                {
                    channel.ExchangeDeclarePassive(queueName);
                }
                catch (OperationInterruptedException)
                {
                    throw new NotSupportedException($"'{queueName}' exchange was not found. Quorum queue migration only supports the conventional routing topology.");
                }

                try
                {
                    // make sure that the queue exists
                    channel.QueueDeclarePassive(queueName);

                    mainQueueExists = true;
                    mainQueueHasMessages = channel.MessageCount(queueName) > 0;

                    channel.QueueDeclare(queueName, true, false, false, quorumQueueArguments);
                    mainQueueIsQuorum = true;
                }
                catch (OperationInterruptedException)
                {
                    channel.Close();
                    channel.Dispose();

                    channel = connection.CreateModel();
                }

                try
                {
                    channel.QueueDeclarePassive(holdingQueueName);

                    holdingQueueExists = true;

                    holdingQueueHasMessages = channel.MessageCount(holdingQueueName) > 0;
                }
                catch (OperationInterruptedException)
                {
                    // This exception is expected when the passive declare fails and shouldn't throw out of this method.
                }
                finally
                {
                    channel.Close();
                    channel.Dispose();
                }
            }
        }
    }
}
