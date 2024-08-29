namespace NServiceBus.Transport.RabbitMQ.CommandLine.Tests.MigrateEndpoint
{
    using System;
    using System.Collections.Generic;
    using System.CommandLine.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Exceptions;
    using NUnit.Framework;

    [TestFixture]
    public class QueueMigrateToQuorumTests
    {
        [Test]
        public async Task Should_blow_up_when_endpoint_queue_does_not_exist()
        {
            var endpointName = "NonExistingEndpoint";

            await CreateExchange(endpointName);

            var ex = Assert.ThrowsAsync<Exception>(async () => await ExecuteMigration(endpointName));

            Assert.That(ex.Message, Does.Contain(endpointName));
        }

        [Test]
        public async Task Should_blow_up_when_no_default_exchange_exists()
        {
            var endpointName = "EndpointWithNoDefaultExchange";

            await CreateQueue(endpointName, quorum: false);

            var ex = Assert.ThrowsAsync<NotSupportedException>(async () => await ExecuteMigration(endpointName));

            Assert.That(ex.Message, Does.Contain(endpointName));
        }

        [Test]
        public async Task Should_convert_queue_to_quorum()
        {
            var endpointName = "EndpointWithClassicQueue";

            await PrepareTestEndpoint(endpointName);

            await ExecuteMigration(endpointName);

            Assert.That(QueueIsQuorum(endpointName), Is.True);
        }

        [Test]
        public async Task Should_handle_failure_after_unbind()
        {
            var endpointName = "FailureAfterUnbind";

            await PrepareTestEndpoint(endpointName);

            await ExecuteBrokerCommand(async (channel, cancellationToken) => await channel.QueueUnbindAsync(endpointName, endpointName, string.Empty, cancellationToken: cancellationToken), CancellationToken.None);

            await ExecuteMigration(endpointName);

            Assert.That(QueueIsQuorum(endpointName), Is.True);
        }

        [Test]
        public async Task Should_preserve_existing_messages()
        {
            var endpointName = "EndpointWithExistingMessages";
            var numExistingMessages = 10;

            await PrepareTestEndpoint(endpointName);

            await AddMessages(endpointName, numExistingMessages);

            await ExecuteMigration(endpointName);

            Assert.Multiple(async () =>
            {
                Assert.That(await QueueIsQuorum(endpointName), Is.True);
                Assert.That(await MessageCount(endpointName), Is.EqualTo(numExistingMessages));
            });
        }

        [Test]
        public async Task Should_preserve_existing_messages_with_messageIds()
        {
            var endpointName = "EndpointWithExistingMessages";
            var numExistingMessages = 10;

            await PrepareTestEndpoint(endpointName);

            await AddMessages(endpointName, numExistingMessages, properties => properties.Headers = new Dictionary<string, object> { { NServiceBus.Headers.MessageId, Guid.NewGuid().ToString() } });

            await ExecuteMigration(endpointName);

            Assert.Multiple(async () =>
            {
                Assert.That(await QueueIsQuorum(endpointName), Is.True);
                Assert.That(await MessageCount(endpointName), Is.EqualTo(numExistingMessages));
            });
        }

        [Test]
        public async Task Should_preserve_existing_messages_in_holding_queue()
        {
            var endpointName = "EndpointWithExistingMessagesInHolding";
            var holdingQueueName = GetHoldingQueueName(endpointName);

            var numExistingMessages = 10;

            await PrepareTestEndpoint(endpointName);

            await CreateQueue(holdingQueueName, quorum: true);
            await AddMessages(holdingQueueName, numExistingMessages);

            await ExecuteMigration(endpointName);

            Assert.Multiple(async () =>
            {
                Assert.That(await QueueIsQuorum(endpointName), Is.True);
                Assert.That(await MessageCount(endpointName), Is.EqualTo(numExistingMessages));
            });
        }

        [Test]
        public async Task Should_deduplicate_when_moving_from_holding()
        {
            var endpointName = "EndpointWithDuplicatesInHolding";
            var holdingQueueName = GetHoldingQueueName(endpointName);

            var numExistingMessages = 10;

            await PrepareTestEndpoint(endpointName);

            await CreateQueue(holdingQueueName, quorum: true);
            await AddMessages(
                holdingQueueName,
                numExistingMessages,
                properties => properties.Headers = new Dictionary<string, object> { { NServiceBus.Headers.MessageId, "duplicate" } });

            await ExecuteMigration(endpointName);

            Assert.Multiple(async () =>
            {
                Assert.That(await QueueIsQuorum(endpointName), Is.True);
                Assert.That(await MessageCount(endpointName), Is.EqualTo(1));
            });
        }

        [Test]
        public async Task Should_succeed_if_holding_queue_exists_with_messages()
        {
            var endpointName = "PartiallyMigratedEndpoint";
            var holdingQueueName = GetHoldingQueueName(endpointName);

            var numExistingMessages = 5;
            var expectedMessageCount = 10;

            await PrepareTestEndpoint(endpointName);
            await AddMessages(endpointName, numExistingMessages);

            await CreateQueue(holdingQueueName, quorum: true);
            await AddMessages(holdingQueueName, numExistingMessages);

            await ExecuteMigration(endpointName);

            Assert.That(await MessageCount(endpointName), Is.EqualTo(expectedMessageCount));
        }

        [Test]
        public async Task Should_succeed_if_main_queue_empty_holding_queue_exists_with_messages()
        {
            var endpointName = "PartiallyMigratedEndpoint_EmptyMain";
            var holdingQueueName = GetHoldingQueueName(endpointName);

            var numExistingMessages = 5;

            await PrepareTestEndpoint(endpointName);

            await CreateQueue(holdingQueueName, quorum: true);
            await AddMessages(holdingQueueName, numExistingMessages);

            await ExecuteMigration(endpointName);

            Assert.Multiple(async () =>
            {
                Assert.That(await QueueIsQuorum(endpointName), Is.True);
                Assert.That(await MessageCount(endpointName), Is.EqualTo(numExistingMessages));
            });
        }

        [Test]
        public async Task Should_succeed_if_main_queue_missing_holding_queue_exists_with_messages()
        {
            var endpointName = "PartiallyMigratedEndpoint_MissingMain";
            var holdingQueueName = GetHoldingQueueName(endpointName);

            var numExistingMessages = 5;

            await CreateExchange(endpointName);

            await TryDeleteQueue(endpointName);

            await CreateQueue(holdingQueueName, quorum: true);
            await AddMessages(holdingQueueName, numExistingMessages);

            await ExecuteMigration(endpointName);

            Assert.Multiple(async () =>
            {
                Assert.That(await QueueIsQuorum(endpointName), Is.True);
                Assert.That(await MessageCount(endpointName), Is.EqualTo(numExistingMessages));
            });
        }

        [Test]
        public async Task Should_succeed_if_empty_quorum_queue_exists_holding_queue_exists_with_messages()
        {
            var endpointName = "PartiallyMigratedEndpoint_EmptyQuorumMain";
            var holdingQueueName = GetHoldingQueueName(endpointName);

            var numExistingMessages = 5;

            await PrepareTestEndpoint(endpointName, true);

            await CreateQueue(holdingQueueName, quorum: true);
            await AddMessages(holdingQueueName, numExistingMessages);

            await ExecuteMigration(endpointName);

            Assert.Multiple(async () =>
            {
                Assert.That(await QueueIsQuorum(endpointName), Is.True);
                Assert.That(await MessageCount(endpointName), Is.EqualTo(numExistingMessages));
            });
        }

        [Test]
        public async Task Should_succeed_if_quorum_queue_exists_with_messages_holding_queue_exists_with_messages()
        {
            var endpointName = "PartiallyMigratedEndpoint_NotEmptyQuorumMain";
            var holdingQueueName = GetHoldingQueueName(endpointName);

            var numExistingMessages = 5;
            var expectedMessageCount = 10;

            await PrepareTestEndpoint(endpointName, true);
            await AddMessages(endpointName, numExistingMessages);

            await CreateQueue(holdingQueueName, quorum: true);
            await AddMessages(holdingQueueName, numExistingMessages);

            await ExecuteMigration(endpointName);

            Assert.Multiple(async () =>
            {
                Assert.That(await QueueIsQuorum(endpointName), Is.True);
                Assert.That(await MessageCount(endpointName), Is.EqualTo(expectedMessageCount));
            });
        }

        [Test]
        public async Task Should_succeed_if_quorum_queue_exists_with_messages_holding_queue_empty()
        {
            var endpointName = "PartiallyMigratedEndpoint_QuorumMainEmptyHoldingQueue";
            var holdingQueueName = GetHoldingQueueName(endpointName);

            await CreateExchange(endpointName);

            await CreateQueue(endpointName, quorum: true);
            await CreateQueue(holdingQueueName, quorum: true);

            await ExecuteMigration(endpointName);

            Assert.Multiple(() =>
            {
                Assert.That(QueueIsQuorum(endpointName), Is.True);
                Assert.That(QueueExists(holdingQueueName), Is.False);
            });
        }

        [SetUp]
        public async Task SetUp()
        {
            var connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString") ?? "host=localhost";

            var connectionFactory = new RabbitMQ.ConnectionFactory("unit-tests", ConnectionConfiguration.Create(connectionString), null, true, false, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30), null);

            brokerConnection = new BrokerConnection(connectionFactory);

            connection = await brokerConnection.Create();
        }

        [TearDown]
        public async Task TearDown()
        {
            if (connection.IsOpen)
            {
                await connection.CloseAsync();
            }
            connection.Dispose();
        }

        Task ExecuteMigration(string endpointName)
        {
            var migrationCommand = new QueueMigrateCommand(endpointName, brokerConnection, new TestConsole());

            return migrationCommand.Run();
        }

        async Task<bool> QueueIsQuorum(string endpointName)
        {
            try
            {
                await CreateQueue(endpointName, quorum: false);
                return false;
            }
            catch (OperationInterruptedException)
            {
                return true;
            }
        }

        async Task PrepareTestEndpoint(string endpointName, bool asQuorumQueue = false)
        {
            await TryDeleteQueue(endpointName);
            await TryDeleteQueue(GetHoldingQueueName(endpointName));

            await CreateQueue(endpointName, quorum: asQuorumQueue);
            await CreateExchange(endpointName);
            await BindQueue(endpointName, endpointName);
        }

        Task TryDeleteQueue(string queueName) => ExecuteBrokerCommand(async (channel, cancellationToken) =>
        {
            try
            {
                await channel.QueueDeleteAsync(queueName, cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
            {
            }
        },
            CancellationToken.None);

        Task CreateQueue(string queueName, bool quorum)
        {
            return ExecuteBrokerCommand(async (channel, cancellationToken) =>
            {
                var queueArguments = new Dictionary<string, object>();

                if (quorum)
                {
                    queueArguments.Add("x-queue-type", "quorum");
                }

                await channel.QueueDeclareAsync(queueName, true, false, false, queueArguments, cancellationToken: cancellationToken);
            },
            CancellationToken.None);
        }

        Task CreateExchange(string exchangeName) => ExecuteBrokerCommand(async (channel, cancellationToken) => await channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Fanout, true, cancellationToken: cancellationToken), CancellationToken.None);

        Task BindQueue(string queueName, string exchangeName) => ExecuteBrokerCommand(async (channel, cancellationToken) => await channel.QueueBindAsync(queueName, exchangeName, string.Empty, cancellationToken: cancellationToken), CancellationToken.None);

        Task AddMessages(string queueName, int numMessages, Action<IBasicProperties> modifications = null)
        {
            return ExecuteBrokerCommand(async (channel, cancellationToken) =>
            {
                await channel.ConfirmSelectAsync(cancellationToken);

                for (var i = 0; i < numMessages; i++)
                {
                    var properties = new BasicProperties();

                    modifications?.Invoke(properties);

                    await channel.BasicPublishAsync(string.Empty, queueName, properties, ReadOnlyMemory<byte>.Empty, true, cancellationToken);
                    await channel.WaitForConfirmsOrDieAsync(cancellationToken);
                }
            },
            CancellationToken.None);
        }

        async Task<uint> MessageCount(string queueName)
        {
            uint messageCount = 0;

            await ExecuteBrokerCommand(async (channel, cancellationToken) => messageCount = await channel.MessageCountAsync(queueName, cancellationToken), CancellationToken.None);

            return messageCount;
        }

        async Task<bool> QueueExists(string queueName)
        {
            bool queueExists = false;

            await ExecuteBrokerCommand(async (channel, cancellationToken) =>
            {
                try
                {
                    await channel.QueueDeclarePassiveAsync(queueName, cancellationToken);
                    queueExists = true;
                }
                catch (OperationInterruptedException)
                {
                    queueExists = false;
                }
            },
            CancellationToken.None);

            return queueExists;
        }

        async Task ExecuteBrokerCommand(Func<IChannel, CancellationToken, Task> command, CancellationToken cancellationToken)
        {
            using var channel = await connection.CreateChannelAsync(cancellationToken);
            await command(channel, cancellationToken);
        }

        static string GetHoldingQueueName(string endpointName) => $"{endpointName}-migration-temp";

        BrokerConnection brokerConnection;
        IConnection connection;
    }
}
