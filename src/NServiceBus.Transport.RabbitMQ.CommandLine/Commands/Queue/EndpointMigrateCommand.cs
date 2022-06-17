namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System;
    using System.CommandLine;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Exceptions;

    class QueueMigrateCommand
    {
        public static Command CreateCommand()
        {
            var command = new Command("migrate-to-quorum", "Migrate an existing classic queue to a quorum queue.");

            var queueNameArgument = new Argument<string>()
            {
                Name = "queueName",
                Description = "Specify the name of the queue to migrate"
            };
            var connectionFactoryBinder = SharedOptions.CreateConnectionFactoryBinderWithOptions(command);

            command.AddArgument(queueNameArgument);

            command.SetHandler(async (string queueName, RabbitMQ.ConnectionFactory connectionFactory, bool useDurableEntities, IConsole console, CancellationToken cancellationToken) =>
            {
                var migrateCommand = new QueueMigrateCommand(queueName, connectionFactory, console);
                await migrateCommand.Run(cancellationToken).ConfigureAwait(false);
            }, queueNameArgument, connectionFactoryBinder);

            return command;
        }

        public QueueMigrateCommand(string queueName, RabbitMQ.ConnectionFactory connectionFactory, IConsole console)
        {
            this.queueName = queueName;
            this.connectionFactory = connectionFactory;
            this.console = console;
            holdingQueueName = $"{queueName}-migration-temp";
            brokerState = new BrokerState();
        }

        public Task Run(CancellationToken cancellationToken = default)
        {
            console.WriteLine($"Starting migration of '{queueName}'");

            using (var connection = connectionFactory.CreateAdministrationConnection())
            {
                brokerState.SetState(queueName, holdingQueueName, connection);

                Validate();

                using (var channel = connection.CreateModel())
                {
                    if (brokerState.MainQueueExists && !brokerState.MainQueueIsQuorum)
                    {
                        MigrateMessagesToHoldingQueue(channel, cancellationToken);
                        DeleteMainQueue(channel);
                    }

                    if (!brokerState.MainQueueExists)
                    {
                        CreateMainQueueAsQuorum(channel);
                    }
                }

                if (brokerState.MainQueueIsQuorum && brokerState.HoldingQueueHasMessages)
                {
                    // Due to https://github.com/rabbitmq/rabbitmq-server/issues/4976 we need to use a separate channel to move
                    // the messages back to the main queue again.
                    using (var channel = connection.CreateModel())
                    {
                        RestoreMessages(channel, cancellationToken);
                    }
                }
            }

            return Task.CompletedTask;
        }

        void Validate()
        {
            if (!brokerState.ExchangeExists)
            {
                throw new NotSupportedException($"'{queueName}' exchange not found. Quorum queue migration is only supports the conventional routing topology.");
            }

            if (!brokerState.MainQueueExists && !brokerState.HoldingQueueExists)
            {
                throw new Exception($"Queue '{queueName}' was not found");
            }

            if (brokerState.MainQueueIsQuorum && !brokerState.HoldingQueueHasMessages)
            {
                throw new Exception($"Queue '{queueName}' is already a quorum queue");
            }
        }


        void MigrateMessagesToHoldingQueue(IModel channel, CancellationToken cancellationToken)
        {
            console.WriteLine($"Migrating main queue messages to holding queue");

            // does the holding queue need to be quorum?
            channel.QueueDeclare(holdingQueueName, true, false, false, quorumQueueArguments);
            console.WriteLine($"Holding queue created: '{holdingQueueName}'");

            // bind the holding queue to the exchange of the queue under migration
            // this will throw if the exchange for the queue doesn't exist
            channel.QueueBind(holdingQueueName, queueName, emptyRoutingKey);
            console.WriteLine($"Holding queue bound to main queue's exchange");

            // unbind the queue under migration to stop more messages from coming in
            channel.QueueUnbind(queueName, queueName, emptyRoutingKey);
            console.WriteLine($"Main queue unbound from exchange");

            brokerState.HoldingQueueExists = true;

            // move all existing messages to the holding queue
            channel.ConfirmSelect();

            var numMessagesMovedToHolding = ProcessMessages(
                channel,
                queueName,
                message =>
                {
                    channel.BasicPublish(emptyRoutingKey, holdingQueueName, message.BasicProperties, message.Body);
                    channel.WaitForConfirmsOrDie();
                    brokerState.HoldingQueueHasMessages = true;
                },
                cancellationToken);

            console.WriteLine($"{numMessagesMovedToHolding} messages moved to the holding queue");
        }

        void DeleteMainQueue(IModel channel)
        {
            if (channel.MessageCount(queueName) > 0)
            {
                throw new Exception($"Queue '{queueName}' is not empty after message processing. This can occur if messages are being published directly to the queue during the migration process.");
            }

            // delete the queue under migration
            channel.QueueDelete(queueName);

            brokerState.MainQueueExists = false;
            brokerState.MainQueueIsQuorum = false;
            brokerState.MainQueueHasMessages = false;

            console.WriteLine($"Main queue removed");
        }

        void CreateMainQueueAsQuorum(IModel channel)
        {
            // recreate the queue
            channel.QueueDeclare(queueName, true, false, false, quorumQueueArguments);
            console.WriteLine($"Main queue recreated as a quorum queue");

            channel.QueueBind(queueName, queueName, emptyRoutingKey);
            console.WriteLine($"Main queue re-bound to its exchange");

            channel.QueueUnbind(holdingQueueName, queueName, emptyRoutingKey);
            console.WriteLine($"Holding queue unbound from main queue's exchange");

            brokerState.MainQueueExists = true;
            brokerState.MainQueueIsQuorum = true;
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

                    channel.BasicPublish(emptyRoutingKey, queueName, message.BasicProperties, message.Body);
                    channel.WaitForConfirmsOrDie();
                    brokerState.MainQueueHasMessages = true;

                    if (messageIdString != null)
                    {
                        messageIds.Add(messageIdString, string.Empty);
                    }
                },
                cancellationToken);

            console.WriteLine($"{numMessageMovedBackToMain} messages moved back to main queue");

            channel.QueueDelete(holdingQueueName);
            brokerState.HoldingQueueExists = false;
            brokerState.HoldingQueueHasMessages = false;

            console.WriteLine($"Holding queue removed");
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
        readonly RabbitMQ.ConnectionFactory connectionFactory;
        readonly IConsole console;
        BrokerState brokerState;

        static string emptyRoutingKey = string.Empty;
        static Dictionary<string, object> quorumQueueArguments = new Dictionary<string, object> { { "x-queue-type", "quorum" } };

        class BrokerState
        {
            public bool ExchangeExists { get; set; } = false;
            public bool MainQueueExists { get; set; } = false;
            public bool MainQueueHasMessages { get; set; } = false;
            public bool MainQueueIsQuorum { get; set; } = false;
            public bool HoldingQueueExists { get; set; } = false;
            public bool HoldingQueueHasMessages { get; set; } = false;

            public void SetState(string queueName, string holdingQueueName, IConnection connection)
            {
                var channel = connection.CreateModel();

                // Make sure the queue exchange exists
                try
                {
                    channel.ExchangeDeclarePassive(queueName);
                    ExchangeExists = true;
                }
                catch (OperationInterruptedException)
                {
                    channel.Close();
                    channel.Dispose();

                    channel = connection.CreateModel();
                }

                try
                {
                    // make sure that the queue exists
                    channel.QueueDeclarePassive(queueName);

                    MainQueueExists = true;
                    MainQueueHasMessages = channel.MessageCount(queueName) > 0;

                    channel.QueueDeclare(queueName, true, false, false, quorumQueueArguments);
                    MainQueueIsQuorum = true;
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
                    HoldingQueueExists = true;
                    HoldingQueueHasMessages = channel.MessageCount(holdingQueueName) > 0;
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