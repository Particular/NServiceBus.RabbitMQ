#nullable enable

namespace NServiceBus.Transport.RabbitMQ.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using NServiceBus.Transport.RabbitMQ.ManagementApi;
    using NUnit.Framework;

    using ConnectionFactory = ConnectionFactory;

    [TestFixture]
    class BrokerVerifierTests
    {
        static readonly string connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString") ?? "host=localhost";
        static readonly ConnectionConfiguration connectionConfiguration = ConnectionConfiguration.Create(connectionString);
        static readonly ConnectionFactory connectionFactory = new(typeof(BrokerVerifierTests).FullName, connectionConfiguration, null, false, false, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(10), []);

        [Test]
        public void Initialize_Should_Get_Response_When_Management_Client_Is_Available_And_Valid()
        {
            var managementClient = new ManagementClient(connectionConfiguration);
            var brokerVerifier = new BrokerVerifier(connectionFactory, true, managementClient);

            Assert.DoesNotThrowAsync(async () => await brokerVerifier.Initialize());
        }

        [Test]
        public void Initialize_Should_Should_Warn_When_Management_Is_Disabled_With_Version_4_Or_Greater()
        {
            var managementClient = new ManagementClient(connectionConfiguration);
            var fakeLogger = new FakeLogger();
            var brokerVerifier = new BrokerVerifier(connectionFactory, false, managementClient, fakeLogger);

            Assert.DoesNotThrowAsync(async () => await brokerVerifier.Initialize());

            Assert.That(fakeLogger.Messages, Has.Exactly(1).Items);
            Assert.That(fakeLogger.Messages.FirstOrDefault(), Does.Contain("Use of RabbitMQ Management API has been disabled."));
        }

        [Test]
        public async Task ValidateDeliveryLimit_Should_Set_Delivery_Limit_Policy()
        {
            var queueName = nameof(ValidateDeliveryLimit_Should_Set_Delivery_Limit_Policy);
            var policyName = $"nsb.{queueName}.delivery-limit";
            await CreateQuorumQueue(queueName);
            var managementClient = new ManagementClient(connectionConfiguration);
            var brokerVerifier = new BrokerVerifier(connectionFactory, true, managementClient);

            await brokerVerifier.Initialize();
            await brokerVerifier.ValidateDeliveryLimit(queueName);

            // It can take some time for updated policies to be applied, so we need to wait.
            // If this test is randomly failing, consider increasing the maxWaitTime
            var maxWaitTime = TimeSpan.FromSeconds(30);
            var pollingInterval = TimeSpan.FromSeconds(2);
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < maxWaitTime)
            {
                var response = await managementClient.GetQueue(queueName);
                if (response.StatusCode == HttpStatusCode.OK
                    && response.Value is not null
                    && response.Value.EffectivePolicyDefinition is not null
                    && response.Value.DeliveryLimit.Equals(-1)
                    && response.Value.AppliedPolicyName == policyName
                    && response.Value.EffectivePolicyDefinition.DeliveryLimit == -1)
                {
                    // Policy applied successfully
                    return;
                }
                await Task.Delay(pollingInterval);
            }

            Assert.Fail($"Policy '{policyName}' was not applied to queue '{queueName}' within {maxWaitTime.TotalSeconds} seconds.");
        }

        [Test]
        public async Task ValidateDeliveryLimit_Should_Throw_When_Queue_Argument_Has_Delivery_Limit_Not_Set_To_Unlimited()
        {
            var queueName = nameof(ValidateDeliveryLimit_Should_Throw_When_Queue_Argument_Has_Delivery_Limit_Not_Set_To_Unlimited);
            var delivery_limit = 5;
            await CreateQuorumQueueWithDeliveryLimit(queueName, delivery_limit);
            var managementClient = new ManagementClient(connectionConfiguration);
            var brokerVerifier = new BrokerVerifier(connectionFactory, true, managementClient);

            await brokerVerifier.Initialize();

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await brokerVerifier.ValidateDeliveryLimit(queueName));
            Assert.That(exception.Message, Does.Contain($"The delivery limit for {queueName} is set to {delivery_limit} by a queue argument. " +
                $"This can interfere with the transport's retry implementation"));
        }

        [Test]
        public async Task ValidateDeliveryLimit_Should_Throw_When_Delivery_Limit_Cannot_Be_Validated()
        {
            var queueName = nameof(ValidateDeliveryLimit_Should_Throw_When_Delivery_Limit_Cannot_Be_Validated);
            await CreateQuorumQueue(queueName);
            var managementClient = new ManagementClient(connectionConfiguration);
            var brokerVerifier = new BrokerVerifier(connectionFactory, true, managementClient);

            await brokerVerifier.Initialize();

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await brokerVerifier.ValidateDeliveryLimit("WrongQueue"));
            Assert.That(exception.Message, Does.Contain($"Could not retrieve full queue details for WrongQueue"));
        }

        [Test]
        public async Task ValidateDeliveryLimit_Should_Throw_When_A_Policy_On_Queue_Has_Delivery_Limit_Not_Set_To_Unlimited()
        {
            // Arrange
            var deliveryLimit = 15;
            var queueName = nameof(ValidateDeliveryLimit_Should_Throw_When_A_Policy_On_Queue_Has_Delivery_Limit_Not_Set_To_Unlimited);
            var managementClient = new ManagementClient(connectionConfiguration);
            var brokerVerifier = new BrokerVerifier(connectionFactory, true, managementClient);
            var policy = new Policy
            {
                Name = $"nsb.{queueName}.delivery-limit",
                ApplyTo = PolicyTarget.QuorumQueues,
                Definition = new PolicyDefinition { DeliveryLimit = deliveryLimit },
                Pattern = queueName,
                Priority = 100
            };

            // Act
            await CreateQuorumQueue(queueName);
            await brokerVerifier.Initialize();
            await managementClient.CreatePolicy(policy).ConfigureAwait(false);
            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await brokerVerifier.ValidateDeliveryLimit(queueName));

            // Assert
            Assert.That(exception.Message, Does.Contain($"The RabbitMQ policy {policy.Name} is setting delivery limit to {deliveryLimit} for {queueName}"));
        }

        static async Task CreateQuorumQueue(string queueName)
        {
            using var connection = await connectionFactory.CreateConnection($"{queueName} connection").ConfigureAwait(false);
            using var channel = await connection.CreateChannelAsync().ConfigureAwait(false);
            var arguments = new Dictionary<string, object?> { { "x-queue-type", "quorum" } };

            _ = await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: arguments);
        }

        static async Task CreateQuorumQueueWithDeliveryLimit(string queueName, int deliveryLimit)
        {
            using var connection = await connectionFactory.CreateConnection($"{queueName} connection").ConfigureAwait(false);
            using var channel = await connection.CreateChannelAsync().ConfigureAwait(false);
            var arguments = new Dictionary<string, object?> { { "x-queue-type", "quorum" }, { "x-delivery-limit", deliveryLimit } };

            _ = await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: arguments);
        }
    }

    class FakeLogger : ILog
    {
        public List<string> Messages { get; } = [];

        public bool IsDebugEnabled => throw new NotImplementedException();

        public bool IsInfoEnabled => throw new NotImplementedException();

        public bool IsWarnEnabled => throw new NotImplementedException();

        public bool IsErrorEnabled => throw new NotImplementedException();

        public bool IsFatalEnabled => throw new NotImplementedException();

        public void Debug(string message, Exception exception) => throw new NotImplementedException();
        public void DebugFormat(string format, params object[] args) => throw new NotImplementedException();
        public void Info(string message, Exception exception) => throw new NotImplementedException();
        public void InfoFormat(string format, params object[] args) => throw new NotImplementedException();
        public void Warn(string message, Exception exception) => Messages.Add(message);
        public void WarnFormat(string format, params object[] args) => Messages.Add(format);
        public void Error(string message, Exception exception) => throw new NotImplementedException();
        public void ErrorFormat(string format, params object[] args) => throw new NotImplementedException();
        public void Fatal(string message, Exception exception) => throw new NotImplementedException();
        public void FatalFormat(string format, params object[] args) => throw new NotImplementedException();
        public void Debug(string message) => throw new NotImplementedException();
        public void Info(string message) => throw new NotImplementedException();
        public void Error(string message) => Messages.Add(message);
        public void Fatal(string message) => throw new NotImplementedException();
        public void Warn(string message) => Messages.Add(message);
    }

}
