#nullable enable

namespace NServiceBus.Transport.RabbitMQ.Tests;

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using NServiceBus.Transport.RabbitMQ.ManagementApi;
using NUnit.Framework;

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
        var brokerVerifier = new BrokerVerifier(managementClient, true);

        Assert.DoesNotThrowAsync(async () => await brokerVerifier.Initialize());
    }

    [Test]
    public async Task ValidateDeliveryLimit_Should_Set_Delivery_Limit_Policy()
    {
        var managementClient = new ManagementClient(connectionConfiguration);
        var brokerVerifier = new BrokerVerifier(managementClient, true);
        await brokerVerifier.Initialize();

        if (brokerVerifier.BrokerVersion < BrokerVerifier.BrokerVersion4)
        {
            Assert.Ignore("Test not valid for broker versions before 4.0.0");
        }

        var queueName = nameof(ValidateDeliveryLimit_Should_Set_Delivery_Limit_Policy);
        var policyName = $"nsb.{queueName}.delivery-limit";
        await CreateQuorumQueue(queueName);

        await brokerVerifier.ValidateDeliveryLimit(queueName);

        // It can take some time for updated policies to be applied, so we need to wait.
        // If this test is randomly failing, consider increasing the attempts
        var attempts = 20;

        for (int i = 0; i < attempts; i++)
        {
            var response = await managementClient.GetQueue(queueName);

            if (response.StatusCode == HttpStatusCode.OK && response.Value?.DeliveryLimit == -1 && response.Value.AppliedPolicyName == policyName)
            {
                // Policy applied successfully
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(3));
        }

        Assert.Fail($"Policy '{policyName}' was not applied to queue '{queueName}'.");
    }

    [Test]
    public async Task ValidateDeliveryLimit_Should_Throw_When_Queue_Does_Not_Exist()
    {
        var queueName = "WrongQueue";
        var managementClient = new ManagementClient(connectionConfiguration);
        var brokerVerifier = new BrokerVerifier(managementClient, true);
        await brokerVerifier.Initialize();

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await brokerVerifier.ValidateDeliveryLimit(queueName));
        Assert.That(exception.Message, Does.Contain($"Could not get queue details for '{queueName}'."));
    }

    [Test]
    public async Task ValidateDeliveryLimit_Should_Throw_When_Queue_Argument_Has_Delivery_Limit_Not_Set_To_Unlimited()
    {
        var queueName = nameof(ValidateDeliveryLimit_Should_Throw_When_Queue_Argument_Has_Delivery_Limit_Not_Set_To_Unlimited);
        var deliveryLimit = 5;
        await CreateQuorumQueue(queueName, deliveryLimit);

        var managementClient = new ManagementClient(connectionConfiguration);
        var brokerVerifier = new BrokerVerifier(managementClient, true);
        await brokerVerifier.Initialize();

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await brokerVerifier.ValidateDeliveryLimit(queueName));
        Assert.That(exception.Message, Does.Contain($"The delivery limit for '{queueName}' is set to the non-default value of '{deliveryLimit}'. Remove any delivery limit settings from queue arguments, user policies or operator policies to correct this."));
    }

    [Test]
    public async Task ValidateDeliveryLimit_Should_Throw_When_A_Policy_On_Queue_Has_Delivery_Limit_Not_Set_To_Unlimited()
    {
        var queueName = nameof(ValidateDeliveryLimit_Should_Throw_When_A_Policy_On_Queue_Has_Delivery_Limit_Not_Set_To_Unlimited);
        await CreateQuorumQueue(queueName);

        var managementClient = new ManagementClient(connectionConfiguration);
        var brokerVerifier = new BrokerVerifier(managementClient, true);
        await brokerVerifier.Initialize();

        var deliveryLimit = 15;
        var policyName = $"nsb.{queueName}.delivery-limit";
        var policy = new Policy
        {
            ApplyTo = PolicyTarget.QuorumQueues,
            Definition = new PolicyDefinition { DeliveryLimit = deliveryLimit },
            Pattern = queueName,
            Priority = 100
        };

        await managementClient.CreatePolicy(policyName, policy).ConfigureAwait(false);

        // If this test appears flaky, a delay should be added here to give the broker some time to apply the policy before calling ValidateDeliveryLimit

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await brokerVerifier.ValidateDeliveryLimit(queueName));
        Assert.That(exception.Message, Does.Contain($"The delivery limit for '{queueName}' is set to the non-default value of '{deliveryLimit}'. Remove any delivery limit settings from queue arguments, user policies or operator policies to correct this."));
    }

    static async Task CreateQuorumQueue(string queueName, int? deliveryLimit = null)
    {
        using var connection = await connectionFactory.CreateAdministrationConnection().ConfigureAwait(false);
        using var channel = await connection.CreateChannelAsync().ConfigureAwait(false);

        var arguments = new Dictionary<string, object?> { { "x-queue-type", "quorum" } };

        if (deliveryLimit is not null)
        {
            arguments.Add("x-delivery-limit", deliveryLimit.Value);
        }

        _ = await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: arguments);
    }
}
