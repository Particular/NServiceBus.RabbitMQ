namespace NServiceBus.Transport.RabbitMQ.CommandLine.Tests.MigrateEndpoint
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Exceptions;
    using NUnit.Framework;

    [TestFixture]
    public class EndpointMigrationTests
    {
        [Test]
        public void Should_blow_up_when_endpoint_queue_does_not_exist()
        {
            var migrationCommand = new MigrateEndpointCommand();
            var endpointName = "NonExistingEndpoint";
            var ex = Assert.ThrowsAsync<OperationInterruptedException>(async () => await migrationCommand.Run(endpointName, ConnectionString, Topology.Conventional, true));

            StringAssert.Contains(endpointName, ex.Message);
        }

        [Test]
        public void Should_blow_up_when_endpoint_queue_already_is_quorum()
        {
            var migrationCommand = new MigrateEndpointCommand();
            var endpointName = "EndpointThatIsAlreadyMigrated";

            CreateQueue(endpointName, quorum: true);

            var ex = Assert.ThrowsAsync<Exception>(async () => await migrationCommand.Run(endpointName, ConnectionString, Topology.Conventional, true));

            StringAssert.Contains(endpointName, ex.Message);
        }

        [Test]
        public void Should_blow_up_when_no_default_exchange_exists()
        {
            var migrationCommand = new MigrateEndpointCommand();
            var endpointName = "EndpointWithNoDefaultExchange";

            CreateQueue(endpointName, quorum: false);

            var ex = Assert.ThrowsAsync<OperationInterruptedException>(async () => await migrationCommand.Run(endpointName, ConnectionString, Topology.Conventional, true));

            StringAssert.Contains(endpointName, ex.Message);
        }

        [Test]
        public async Task Should_convert_queue_to_quorum()
        {
            var migrationCommand = new MigrateEndpointCommand();
            var endpointName = "EndpointWithClassicQueue";

            PrepareTestEndpoint(endpointName);

            await migrationCommand.Run(endpointName, ConnectionString, Topology.Conventional, true);

            Assert.Throws<OperationInterruptedException>(() => CreateQueue(endpointName, quorum: false));
        }

        [Test]
        public async Task Should_preserve_existing_messages()
        {
            var migrationCommand = new MigrateEndpointCommand();
            var endpointName = "EndpointWithExistingMessages";
            var numExistingMessage = 10;

            PrepareTestEndpoint(endpointName);

            AddMessages(endpointName, numExistingMessage);

            await migrationCommand.Run(endpointName, ConnectionString, Topology.Conventional, true);

            Assert.AreEqual(numExistingMessage, MessageCount(endpointName));
        }

        [Test]
        public async Task Should_preserve_existing_messages_in_holding_queue()
        {
            var migrationCommand = new MigrateEndpointCommand();
            var endpointName = "EndpointWithExistingMessagesInHolding";
            var holdingQueueName = GetHoldingQueueName(endpointName);

            var numExistingMessage = 10;

            PrepareTestEndpoint(endpointName);

            CreateQueue(holdingQueueName, quorum: true);
            AddMessages(holdingQueueName, numExistingMessage);

            await migrationCommand.Run(endpointName, ConnectionString, Topology.Conventional, true);

            Assert.AreEqual(numExistingMessage, MessageCount(endpointName));
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
            CommandRunner.Run(ConnectionString, channel =>
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
            CommandRunner.Run(ConnectionString, channel =>
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
            CommandRunner.Run(ConnectionString, channel =>
            {
                channel.ExchangeDeclare(exchangeName, ExchangeType.Fanout, true);
            });
        }

        void BindQueue(string queueName, string exchangeName)
        {
            CommandRunner.Run(ConnectionString, channel =>
            {
                channel.QueueBind(queueName, exchangeName, string.Empty);
            });
        }

        void AddMessages(string queueName, int numMessages)
        {
            CommandRunner.Run(ConnectionString, channel =>
            {
                for (var i = 0; i < numMessages; i++)
                {
                    var properties = channel.CreateBasicProperties();
                    channel.BasicPublish(string.Empty, queueName, true, properties, ReadOnlyMemory<byte>.Empty);
                }
            });
        }

        uint MessageCount(string queueName)
        {
            uint messageCount = 0;
            CommandRunner.Run(ConnectionString, channel =>
            {
                messageCount = channel.MessageCount(queueName);
            });

            return messageCount;
        }

        string GetHoldingQueueName(string endpointName)
        {
            return $"{endpointName}-migration-temp";
        }

        static string ConnectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString") ?? "host=localhost";
    }
}
