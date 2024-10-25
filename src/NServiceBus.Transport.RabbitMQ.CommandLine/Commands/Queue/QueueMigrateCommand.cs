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
                await migrateCommand.Run(cancellationToken);
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

        public async Task Run(CancellationToken cancellationToken = default)
        {
            console.WriteLine($"Starting migration of '{queueName}'");

            using var connection = await brokerConnection.Create(cancellationToken);

            await migrationState.SetInitialMigrationStage(queueName, holdingQueueName, connection, cancellationToken);

            while (migrationState.CurrentStage != MigrationStage.CleanUpCompleted)
            {
                switch (migrationState.CurrentStage)
                {
                    case MigrationStage.Starting:
                        migrationState.CurrentStage = await MoveMessagesToHoldingQueue(connection, cancellationToken);
                        break;
                    case MigrationStage.MessagesMovedToHoldingQueue:
                        migrationState.CurrentStage = await DeleteMainQueue(connection, cancellationToken);
                        break;
                    case MigrationStage.ClassicQueueDeleted:
                        migrationState.CurrentStage = await CreateMainQueueAsQuorum(connection, cancellationToken);
                        break;
                    case MigrationStage.QuorumQueueCreated:
                        migrationState.CurrentStage = await RestoreMessages(connection, cancellationToken);
                        break;
                    case MigrationStage.MessagesMovedToQuorumQueue:
                        migrationState.CurrentStage = await CleanUpHoldingQueue(connection, cancellationToken);
                        break;
                    case MigrationStage.CleanUpCompleted:
                    default:
                        break;
                }
            }
        }

        async Task<MigrationStage> MoveMessagesToHoldingQueue(IConnection connection, CancellationToken cancellationToken)
        {
            var createChannelOptions = new CreateChannelOptions(publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true,
                outstandingPublisherConfirmationsRateLimiter: null);
            await using var channel = await connection.CreateChannelAsync(createChannelOptions,
                cancellationToken: cancellationToken);

            console.WriteLine($"Migrating messages from '{queueName}' to '{holdingQueueName}'");

            // does the holding queue need to be quorum?
            await channel.QueueDeclareAsync(holdingQueueName, true, false, false, quorumQueueArguments, cancellationToken: cancellationToken);
            console.WriteLine($"Created queue '{holdingQueueName}'");

            // bind the holding queue to the exchange of the queue under migration
            // this will throw if the exchange for the queue doesn't exist
            await channel.QueueBindAsync(holdingQueueName, queueName, string.Empty, cancellationToken: cancellationToken);
            console.WriteLine($"Bound '{holdingQueueName}' to exchange '{queueName}'");

            // unbind the queue under migration to stop more messages from coming in
            await channel.QueueUnbindAsync(queueName, queueName, string.Empty, cancellationToken: cancellationToken);
            console.WriteLine($"Unbound '{queueName}' from exchange '{queueName}' ");

            // move all existing messages to the holding queue
            var numMessagesMovedToHolding = await ProcessMessages(
                channel,
                queueName,
                async (message, token) =>
                {
                    await channel.BasicPublishAsync(string.Empty, holdingQueueName, false, new BasicProperties(message.BasicProperties), message.Body, cancellationToken: token);
                },
                cancellationToken);

            console.WriteLine($"Moved {numMessagesMovedToHolding} messages to '{holdingQueueName}'");

            return MigrationStage.MessagesMovedToHoldingQueue;
        }

        async Task<MigrationStage> DeleteMainQueue(IConnection connection, CancellationToken cancellationToken)
        {
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            if (await channel.MessageCountAsync(queueName, cancellationToken) > 0)
            {
                throw new Exception($"Queue '{queueName}' is not empty after message processing. This can occur if messages are being published directly to the queue during the migration process. Run the command again to retry message processing.");
            }

            // delete the queue under migration
            await channel.QueueDeleteAsync(queueName, cancellationToken: cancellationToken);
            console.WriteLine($"Removed '{queueName}'");

            return MigrationStage.ClassicQueueDeleted;
        }

        async Task<MigrationStage> CreateMainQueueAsQuorum(IConnection connection, CancellationToken cancellationToken)
        {
            using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            await channel.QueueDeclareAsync(queueName, true, false, false, quorumQueueArguments, cancellationToken: cancellationToken);
            console.WriteLine($"Recreated '{queueName}' as a quorum queue");

            return MigrationStage.QuorumQueueCreated;
        }

        async Task<MigrationStage> RestoreMessages(IConnection connection, CancellationToken cancellationToken)
        {
            var createChannelOptions = new CreateChannelOptions(publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true,
                outstandingPublisherConfirmationsRateLimiter: null);
            await using var channel = await connection.CreateChannelAsync(createChannelOptions,
                cancellationToken: cancellationToken);

            await channel.QueueBindAsync(queueName, queueName, string.Empty, cancellationToken: cancellationToken);
            console.WriteLine($"Re-bound '{queueName}' to exchange '{queueName}'");

            await channel.QueueUnbindAsync(holdingQueueName, queueName, string.Empty, cancellationToken: cancellationToken);
            console.WriteLine($"Unbound '{holdingQueueName}' from exchange '{queueName}'");

            var messageIds = new Dictionary<string, string>();

            // move all messages in the holding queue back to the main queue
            var numMessageMovedBackToMain = await ProcessMessages(
                channel,
                holdingQueueName,
                async (message, token) =>
                {
                    string? messageIdString = null;

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

                    await channel.BasicPublishAsync(string.Empty, queueName, false, new BasicProperties(message.BasicProperties), message.Body, cancellationToken: token);

                    if (messageIdString != null)
                    {
                        messageIds.Add(messageIdString, string.Empty);
                    }
                },
                cancellationToken);

            console.WriteLine($"Moved {numMessageMovedBackToMain} messages from '{holdingQueueName}' to '{queueName}'");

            return MigrationStage.MessagesMovedToQuorumQueue;
        }

        async Task<MigrationStage> CleanUpHoldingQueue(IConnection connection, CancellationToken cancellationToken)
        {
            using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            if (await channel.MessageCountAsync(holdingQueueName, cancellationToken) != 0)
            {
                throw new Exception($"'{holdingQueueName}' is not empty and was not deleted. Run the command again to retry message processing.");
            }

            await channel.QueueDeleteAsync(holdingQueueName, cancellationToken: cancellationToken);
            console.WriteLine($"Removed '{holdingQueueName}'");

            return MigrationStage.CleanUpCompleted;
        }

        async Task<uint> ProcessMessages(IChannel channel, string sourceQueue, Func<BasicGetResult, CancellationToken, Task> onMoveMessage, CancellationToken cancellationToken)
        {
            var messageCount = await channel.MessageCountAsync(sourceQueue, cancellationToken);

            for (var i = 0; i < messageCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var message = await channel.BasicGetAsync(sourceQueue, false, cancellationToken);

                if (message == null)
                {
                    // Queue is empty
                    break;
                }

                await onMoveMessage(message, cancellationToken);

                await channel.BasicAckAsync(message.DeliveryTag, false, cancellationToken);
            }

            return messageCount;
        }

        readonly string queueName;
        readonly string holdingQueueName;
        readonly BrokerConnection brokerConnection;
        readonly IConsole console;
        readonly MigrationState migrationState;

        static Dictionary<string, object?> quorumQueueArguments = new() { { "x-queue-type", "quorum" } };

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

            public async Task SetInitialMigrationStage(string queueName, string holdingQueueName, IConnection connection, CancellationToken cancellationToken = default)
            {
                await SetBrokerState(queueName, holdingQueueName, connection, cancellationToken);

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

            async Task SetBrokerState(string queueName, string holdingQueueName, IConnection connection, CancellationToken cancellationToken)
            {
                var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

                try
                {
                    await channel.ExchangeDeclarePassiveAsync(queueName, cancellationToken);
                }
                catch (OperationInterruptedException)
                {
                    throw new NotSupportedException($"'{queueName}' exchange was not found. Quorum queue migration only supports the conventional routing topology.");
                }

                try
                {
                    // make sure that the queue exists
                    await channel.QueueDeclarePassiveAsync(queueName, cancellationToken);

                    mainQueueExists = true;
                    mainQueueHasMessages = await channel.MessageCountAsync(queueName, cancellationToken) > 0;

                    await channel.QueueDeclareAsync(queueName, true, false, false, quorumQueueArguments, cancellationToken: cancellationToken);
                    mainQueueIsQuorum = true;
                }
                catch (OperationInterruptedException)
                {
                    await channel.CloseAsync(cancellationToken);
                    channel.Dispose();

                    channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
                }

                try
                {
                    await channel.QueueDeclarePassiveAsync(holdingQueueName, cancellationToken);

                    holdingQueueExists = true;

                    holdingQueueHasMessages = await channel.MessageCountAsync(holdingQueueName, cancellationToken) > 0;
                }
                catch (OperationInterruptedException)
                {
                    // This exception is expected when the passive declare fails and shouldn't throw out of this method.
                }
                finally
                {
                    await channel.CloseAsync(cancellationToken);
                    channel.Dispose();
                }
            }
        }
    }
}
