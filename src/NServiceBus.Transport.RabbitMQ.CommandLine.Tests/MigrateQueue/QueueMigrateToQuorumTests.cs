﻿namespace NServiceBus.Transport.RabbitMQ.CommandLine.Tests.MigrateEndpoint
{
    using System;
    using System.Collections.Generic;
    using System.CommandLine.IO;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Exceptions;
    using NUnit.Framework;

    [TestFixture]
    public class QueueMigrateToQuorumTests
    {
        [Test]
        public void Should_blow_up_when_endpoint_queue_does_not_exist()
        {
            var endpointName = "NonExistingEndpoint";

            CreateExchange(endpointName);

            var ex = Assert.ThrowsAsync<Exception>(async () => await ExecuteMigration(endpointName));

            Assert.That(ex.Message, Does.Contain(endpointName));
        }

        [Test]
        public void Should_blow_up_when_no_default_exchange_exists()
        {
            var endpointName = "EndpointWithNoDefaultExchange";

            CreateQueue(endpointName, quorum: false);

            var ex = Assert.ThrowsAsync<NotSupportedException>(async () => await ExecuteMigration(endpointName));

            Assert.That(ex.Message, Does.Contain(endpointName));
        }

        [Test]
        public async Task Should_convert_queue_to_quorum()
        {
            var endpointName = "EndpointWithClassicQueue";

            PrepareTestEndpoint(endpointName);

            await ExecuteMigration(endpointName);

            Assert.That(QueueIsQuorum(endpointName), Is.True);
        }

        [Test]
        public async Task Should_handle_failure_after_unbind()
        {
            var endpointName = "FailureAfterUnbind";

            PrepareTestEndpoint(endpointName);

            ExecuteBrokerCommand(channel => channel.QueueUnbind(endpointName, endpointName, string.Empty));

            await ExecuteMigration(endpointName);

            Assert.That(QueueIsQuorum(endpointName), Is.True);
        }

        [Test]
        public async Task Should_preserve_existing_messages()
        {
            var endpointName = "EndpointWithExistingMessages";
            var numExistingMessages = 10;

            PrepareTestEndpoint(endpointName);

            AddMessages(endpointName, numExistingMessages);

            await ExecuteMigration(endpointName);

            Assert.Multiple(() =>
            {
                Assert.That(QueueIsQuorum(endpointName), Is.True);
                Assert.That(MessageCount(endpointName), Is.EqualTo(numExistingMessages));
            });
        }

        [Test]
        public async Task Should_preserve_existing_messages_with_messageIds()
        {
            var endpointName = "EndpointWithExistingMessages";
            var numExistingMessages = 10;

            PrepareTestEndpoint(endpointName);

            AddMessages(endpointName, numExistingMessages, properties => properties.Headers = new Dictionary<string, object> { { NServiceBus.Headers.MessageId, Guid.NewGuid().ToString() } });

            await ExecuteMigration(endpointName);

            Assert.Multiple(() =>
            {
                Assert.That(QueueIsQuorum(endpointName), Is.True);
                Assert.That(MessageCount(endpointName), Is.EqualTo(numExistingMessages));
            });
        }

        [Test]
        public async Task Should_preserve_existing_messages_in_holding_queue()
        {
            var endpointName = "EndpointWithExistingMessagesInHolding";
            var holdingQueueName = GetHoldingQueueName(endpointName);

            var numExistingMessages = 10;

            PrepareTestEndpoint(endpointName);

            CreateQueue(holdingQueueName, quorum: true);
            AddMessages(holdingQueueName, numExistingMessages);

            await ExecuteMigration(endpointName);

            Assert.Multiple(() =>
            {
                Assert.That(QueueIsQuorum(endpointName), Is.True);
                Assert.That(MessageCount(endpointName), Is.EqualTo(numExistingMessages));
            });
        }

        [Test]
        public async Task Should_deduplicate_when_moving_from_holding()
        {
            var endpointName = "EndpointWithDuplicatesInHolding";
            var holdingQueueName = GetHoldingQueueName(endpointName);

            var numExistingMessages = 10;

            PrepareTestEndpoint(endpointName);

            CreateQueue(holdingQueueName, quorum: true);
            AddMessages(
                holdingQueueName,
                numExistingMessages,
                properties => properties.Headers = new Dictionary<string, object> { { NServiceBus.Headers.MessageId, "duplicate" } });

            await ExecuteMigration(endpointName);

            Assert.Multiple(() =>
            {
                Assert.That(QueueIsQuorum(endpointName), Is.True);
                Assert.That(MessageCount(endpointName), Is.EqualTo(1));
            });
        }

        [Test]
        public async Task Should_succeed_if_holding_queue_exists_with_messages()
        {
            var endpointName = "PartiallyMigratedEndpoint";
            var holdingQueueName = GetHoldingQueueName(endpointName);

            var numExistingMessages = 5;
            var expectedMessageCount = 10;

            PrepareTestEndpoint(endpointName);
            AddMessages(endpointName, numExistingMessages);

            CreateQueue(holdingQueueName, quorum: true);
            AddMessages(holdingQueueName, numExistingMessages);

            await ExecuteMigration(endpointName);

            Assert.That(MessageCount(endpointName), Is.EqualTo(expectedMessageCount));
        }

        [Test]
        public async Task Should_succeed_if_main_queue_empty_holding_queue_exists_with_messages()
        {
            var endpointName = "PartiallyMigratedEndpoint_EmptyMain";
            var holdingQueueName = GetHoldingQueueName(endpointName);

            var numExistingMessages = 5;

            PrepareTestEndpoint(endpointName);

            CreateQueue(holdingQueueName, quorum: true);
            AddMessages(holdingQueueName, numExistingMessages);

            await ExecuteMigration(endpointName);

            Assert.Multiple(() =>
            {
                Assert.That(QueueIsQuorum(endpointName), Is.True);
                Assert.That(MessageCount(endpointName), Is.EqualTo(numExistingMessages));
            });
        }

        [Test]
        public async Task Should_succeed_if_main_queue_missing_holding_queue_exists_with_messages()
        {
            var endpointName = "PartiallyMigratedEndpoint_MissingMain";
            var holdingQueueName = GetHoldingQueueName(endpointName);

            var numExistingMessages = 5;

            CreateExchange(endpointName);

            TryDeleteQueue(endpointName);

            CreateQueue(holdingQueueName, quorum: true);
            AddMessages(holdingQueueName, numExistingMessages);

            await ExecuteMigration(endpointName);

            Assert.Multiple(() =>
            {
                Assert.That(QueueIsQuorum(endpointName), Is.True);
                Assert.That(MessageCount(endpointName), Is.EqualTo(numExistingMessages));
            });
        }

        [Test]
        public async Task Should_succeed_if_empty_quorum_queue_exists_holding_queue_exists_with_messages()
        {
            var endpointName = "PartiallyMigratedEndpoint_EmptyQuorumMain";
            var holdingQueueName = GetHoldingQueueName(endpointName);

            var numExistingMessages = 5;

            PrepareTestEndpoint(endpointName, true);

            CreateQueue(holdingQueueName, quorum: true);
            AddMessages(holdingQueueName, numExistingMessages);

            await ExecuteMigration(endpointName);

            Assert.Multiple(() =>
            {
                Assert.That(QueueIsQuorum(endpointName), Is.True);
                Assert.That(MessageCount(endpointName), Is.EqualTo(numExistingMessages));
            });
        }

        [Test]
        public async Task Should_succeed_if_quorum_queue_exists_with_messages_holding_queue_exists_with_messages()
        {
            var endpointName = "PartiallyMigratedEndpoint_NotEmptyQuorumMain";
            var holdingQueueName = GetHoldingQueueName(endpointName);

            var numExistingMessages = 5;
            var expectedMessageCount = 10;

            PrepareTestEndpoint(endpointName, true);
            AddMessages(endpointName, numExistingMessages);

            CreateQueue(holdingQueueName, quorum: true);
            AddMessages(holdingQueueName, numExistingMessages);

            await ExecuteMigration(endpointName);

            Assert.Multiple(() =>
            {
                Assert.That(QueueIsQuorum(endpointName), Is.True);
                Assert.That(MessageCount(endpointName), Is.EqualTo(expectedMessageCount));
            });
        }

        [Test]
        public async Task Should_succeed_if_quorum_queue_exists_with_messages_holding_queue_empty()
        {
            var endpointName = "PartiallyMigratedEndpoint_QuorumMainEmptyHoldingQueue";
            var holdingQueueName = GetHoldingQueueName(endpointName);

            CreateExchange(endpointName);

            CreateQueue(endpointName, quorum: true);
            CreateQueue(holdingQueueName, quorum: true);

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

        bool QueueIsQuorum(string endpointName)
        {
            try
            {
                CreateQueue(endpointName, quorum: false);
                return false;
            }
            catch (OperationInterruptedException)
            {
                return true;
            }
        }

        void PrepareTestEndpoint(string endpointName, bool asQuorumQueue = false)
        {
            TryDeleteQueue(endpointName);
            TryDeleteQueue(GetHoldingQueueName(endpointName));

            CreateQueue(endpointName, quorum: asQuorumQueue);
            CreateExchange(endpointName);
            BindQueue(endpointName, endpointName);
        }

        void TryDeleteQueue(string queueName) => ExecuteBrokerCommand(channel =>
        {
            try
            {
                channel.QueueDelete(queueName);
            }
            catch (Exception)
            {
            }
        });

        void CreateQueue(string queueName, bool quorum)
        {
            ExecuteBrokerCommand(channel =>
            {
                var queueArguments = new Dictionary<string, object>();

                if (quorum)
                {
                    queueArguments.Add("x-queue-type", "quorum");
                }

                channel.QueueDeclare(queueName, true, false, false, queueArguments);
            });
        }

        void CreateExchange(string exchangeName) => ExecuteBrokerCommand(channel => channel.ExchangeDeclare(exchangeName, ExchangeType.Fanout, true));

        void BindQueue(string queueName, string exchangeName)
        {
            ExecuteBrokerCommand(channel => channel.QueueBind(queueName, exchangeName, string.Empty));
        }

        void AddMessages(string queueName, int numMessages, Action<IBasicProperties> modifications = null)
        {
            ExecuteBrokerCommand(channel =>
            {
                channel.ConfirmSelect();

                for (var i = 0; i < numMessages; i++)
                {
                    var properties = channel.CreateBasicProperties();

                    modifications?.Invoke(properties);

                    channel.BasicPublish(string.Empty, queueName, true, properties, ReadOnlyMemory<byte>.Empty);
                    channel.WaitForConfirmsOrDie();
                }
            });
        }

        async Task<uint> MessageCount(string queueName)
        {
            uint messageCount = 0;

            await ExecuteBrokerCommand(async channel => messageCount = await channel.MessageCountAsync(queueName));

            return messageCount;
        }

        async Task<bool> QueueExists(string queueName)
        {
            bool queueExists = false;

            await ExecuteBrokerCommand(async channel =>
            {
                try
                {
                    await channel.QueueDeclarePassiveAsync(queueName);
                    queueExists = true;
                }
                catch (OperationInterruptedException)
                {
                    queueExists = false;
                }
            });

            return queueExists;
        }

        async Task ExecuteBrokerCommand(Func<IChannel, Task> command)
        {
            using var channel = await connection.CreateChannelAsync();
            await command(channel);
        }

        static string GetHoldingQueueName(string endpointName) => $"{endpointName}-migration-temp";

        BrokerConnection brokerConnection;
        IConnection connection;
    }
}
