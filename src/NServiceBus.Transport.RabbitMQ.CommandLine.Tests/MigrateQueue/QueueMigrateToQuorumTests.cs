namespace NServiceBus.Transport.RabbitMQ.CommandLine.Tests.MigrateEndpoint
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

            StringAssert.Contains(endpointName, ex.Message);
        }

        [Test]
        public void Should_blow_up_when_no_default_exchange_exists()
        {
            var endpointName = "EndpointWithNoDefaultExchange";

            CreateQueue(endpointName, quorum: false);

            var ex = Assert.ThrowsAsync<NotSupportedException>(async () => await ExecuteMigration(endpointName));

            StringAssert.Contains(endpointName, ex.Message);
        }

        [Test]
        public async Task Should_convert_queue_to_quorum()
        {
            var endpointName = "EndpointWithClassicQueue";

            PrepareTestEndpoint(endpointName);

            await ExecuteMigration(endpointName);

            Assert.True(QueueIsQuorum(endpointName));
        }

        [Test]
        public async Task Should_handle_failure_after_unbind()
        {
            var endpointName = "FailureAfterUnbind";

            PrepareTestEndpoint(endpointName);

            ExecuteBrokerCommand(channel =>
            {
                channel.QueueUnbind(endpointName, endpointName, string.Empty);
            });

            await ExecuteMigration(endpointName);

            Assert.True(QueueIsQuorum(endpointName));
        }

        [Test]
        public async Task Should_preserve_existing_messages()
        {
            var endpointName = "EndpointWithExistingMessages";
            var numExistingMessages = 10;

            PrepareTestEndpoint(endpointName);

            AddMessages(endpointName, numExistingMessages);

            await ExecuteMigration(endpointName);

            Assert.True(QueueIsQuorum(endpointName));
            Assert.AreEqual(numExistingMessages, MessageCount(endpointName));
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

            Assert.True(QueueIsQuorum(endpointName));
            Assert.AreEqual(numExistingMessages, MessageCount(endpointName));
        }

        [Test]
        public async Task Should_deduplicate_when_moving_from_holding()
        {
            var endpointName = "EndpointWithDuplicatesInHolding";
            var holdingQueueName = GetHoldingQueueName(endpointName);

            var numExistingMessages = 10;

            PrepareTestEndpoint(endpointName);

            CreateQueue(holdingQueueName, quorum: true);
            AddMessages(holdingQueueName, numExistingMessages, properties =>
            {
                properties.Headers = new Dictionary<string, object> { { NServiceBus.Headers.MessageId, "duplicate" } };
            });

            await ExecuteMigration(endpointName);

            Assert.True(QueueIsQuorum(endpointName));
            Assert.AreEqual(1, MessageCount(endpointName));
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

            Assert.AreEqual(expectedMessageCount, MessageCount(endpointName));
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

            Assert.True(QueueIsQuorum(endpointName));
            Assert.AreEqual(numExistingMessages, MessageCount(endpointName));
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

            Assert.True(QueueIsQuorum(endpointName));
            Assert.AreEqual(numExistingMessages, MessageCount(endpointName));
        }

        [Test]
        public async Task Should_succeed_if_empty_quorum_queue_exists_holding_queue_exists_with_messages()
        {
            var endpointName = "PartiallyMigratedEndpoint_EmptyQuorumMain";
            var holdingQueueName = GetHoldingQueueName(endpointName);

            var numExistingMessages = 5;

            CreateExchange(endpointName);

            CreateQueue(endpointName, quorum: true);
            CreateQueue(holdingQueueName, quorum: true);

            AddMessages(holdingQueueName, numExistingMessages);

            await ExecuteMigration(endpointName);

            Assert.True(QueueIsQuorum(endpointName));
            Assert.AreEqual(numExistingMessages, MessageCount(endpointName));
        }

        [Test]
        public async Task Should_succeed_if_quorum_queue_exists_with_messages_holding_queue_exists_with_messages()
        {
            var endpointName = "PartiallyMigratedEndpoint_NotEmptyQuorumMain";
            var holdingQueueName = GetHoldingQueueName(endpointName);

            var numExistingMessages = 5;
            var expectedMessageCount = 10;

            CreateExchange(endpointName);

            CreateQueue(endpointName, quorum: true);

            AddMessages(endpointName, numExistingMessages);

            CreateQueue(holdingQueueName, quorum: true);

            AddMessages(holdingQueueName, numExistingMessages);

            await ExecuteMigration(endpointName);

            Assert.True(QueueIsQuorum(endpointName));
            Assert.AreEqual(expectedMessageCount, MessageCount(endpointName));
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

            Assert.True(QueueIsQuorum(endpointName));
            Assert.False(QueueExists(holdingQueueName));
        }

        [SetUp]
        public void SetUp()
        {
            var connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString") ?? "host=localhost";

            var connectionFactory = new RabbitMQ.ConnectionFactory("unit-tests", ConnectionConfiguration.Create(connectionString), null, true, false, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30), null);

            brokerConnection = new BrokerConnection(connectionFactory);

            connection = brokerConnection.Create();
        }

        [TearDown]
        public void TearDown()
        {
            connection.Close();
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

        void PrepareTestEndpoint(string endpointName)
        {
            TryDeleteQueue(endpointName);
            TryDeleteQueue(GetHoldingQueueName(endpointName));

            CreateQueue(endpointName, quorum: false);
            CreateExchange(endpointName);
            BindQueue(endpointName, endpointName);
        }

        void TryDeleteQueue(string queueName)
        {
            ExecuteBrokerCommand(channel =>
            {
                try
                {
                    channel.QueueDelete(queueName);
                }
                catch (Exception)
                {
                }
            });
        }

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

        void CreateExchange(string exchangeName)
        {
            ExecuteBrokerCommand(channel =>
            {
                channel.ExchangeDeclare(exchangeName, ExchangeType.Fanout, true);
            });
        }

        void BindQueue(string queueName, string exchangeName)
        {
            ExecuteBrokerCommand(channel =>
            {
                channel.QueueBind(queueName, exchangeName, string.Empty);
            });
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

        uint MessageCount(string queueName)
        {
            uint messageCount = 0;

            ExecuteBrokerCommand(channel =>
            {
                messageCount = channel.MessageCount(queueName);
            });

            return messageCount;
        }

        bool QueueExists(string queueName)
        {
            bool queueExists = false;

            ExecuteBrokerCommand(channel =>
            {
                try
                {
                    channel.QueueDeclarePassive(queueName);
                    queueExists = true;
                }
                catch (OperationInterruptedException)
                {
                    queueExists = false;
                }
            });

            return queueExists;
        }

        void ExecuteBrokerCommand(Action<IModel> command)
        {
            using (var channel = connection.CreateModel())
            {
                command(channel);
            }
        }

        string GetHoldingQueueName(string endpointName)
        {
            return $"{endpointName}-migration-temp";
        }

        BrokerConnection brokerConnection;
        IConnection connection;
    }
}
