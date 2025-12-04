#nullable enable

namespace NServiceBus.Transport.RabbitMQ.Tests;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NServiceBus.Transport.RabbitMQ.ManagementApi;
using NUnit.Framework;

[TestFixture]
class BrokerVerifierTests
{
    static readonly string connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString") ?? "host=localhost";
    static readonly ConnectionConfiguration connectionConfiguration = ConnectionConfiguration.Create(connectionString);
    static readonly ConnectionFactory connectionFactory = new(typeof(BrokerVerifierTests).FullName ?? string.Empty, connectionConfiguration, null, false, false, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(10), []);

    [Test]
    public void Initialize_Should_Get_Response_When_Management_Client_Is_Available_And_Valid()
    {
        var managementClient = new ManagementClient(connectionConfiguration);
        using var brokerVerifier = new BrokerVerifier(managementClient, BrokerRequirementChecks.None, true);

        Assert.DoesNotThrowAsync(async () => await brokerVerifier.Initialize());
    }

    [Test]
    public async Task ValidateDeliveryLimit_Should_Set_Delivery_Limit_Policy()
    {
        var managementClient = new ManagementClient(connectionConfiguration);
        using var brokerVerifier = new BrokerVerifier(managementClient, BrokerRequirementChecks.None, true);
        await brokerVerifier.Initialize();

        if (brokerVerifier.BrokerVersion < BrokerVerifier.BrokerVersion4)
        {
            Assert.Ignore("Test not valid for broker versions before 4.0.0");
        }

        var queueName = nameof(ValidateDeliveryLimit_Should_Set_Delivery_Limit_Policy);
        var policyName = $"nsb.{queueName}.delivery-limit";
        await CreateQueue(queueName);

        await brokerVerifier.ValidateDeliveryLimit(queueName);

        // It can take some time for updated policies to be applied, so we need to wait.
        // If this test is randomly failing, consider increasing the attempts
        var attempts = 20;

        for (int i = 0; i < attempts; i++)
        {
            var queue = await managementClient.GetQueue(queueName);
            var deliveryLimit = queue.GetDeliveryLimit();

            if (deliveryLimit == Queue.BigValueInsteadOfActuallyUnlimited && queue.AppliedPolicyName == policyName)
            {
                // Policy applied successfully
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(3));
        }

        Assert.Fail($"Policy '{policyName}' was not applied to queue '{queueName}'.");
    }

    [Test]
    public async Task ValidateDeliveryLimit_Should_Update_Old_Unlimited_Policy_Created_By_Transport()
    {
        var managementClient = new ManagementClient(connectionConfiguration);
        using var brokerVerifier = new BrokerVerifier(managementClient, BrokerRequirementChecks.None, true);
        await brokerVerifier.Initialize();

        if (brokerVerifier.BrokerVersion < BrokerVerifier.BrokerVersion4)
        {
            Assert.Ignore("Test not valid for broker versions before 4.0.0");
        }

        var queueName = nameof(ValidateDeliveryLimit_Should_Set_Delivery_Limit_Policy);
        var policyName = $"nsb.{queueName}.delivery-limit";

        var oldUnlimitedPolicy = new Policy
        {
            ApplyTo = PolicyTarget.QuorumQueues,
            Definition = new PolicyDefinition { DeliveryLimit = -1 },
            Pattern = queueName,
            Priority = 0
        };

        await CreateQueue(queueName);
        await managementClient.CreatePolicy(policyName, oldUnlimitedPolicy);

        // It can take some time for updated policies to be applied, so we need to wait.
        // If this test is randomly failing, consider increasing the attempts
        var attempts = 20;

        int deliveryLimit = 0;

        for (int i = 0; i < attempts; i++)
        {
            var queue = await managementClient.GetQueue(queueName);
            deliveryLimit = queue.GetDeliveryLimit();

            if (deliveryLimit == -1 && queue.AppliedPolicyName == policyName)
            {
                // Policy applied successfully
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(3));
        }

        if (deliveryLimit != -1)
        {
            Assert.Fail($"Old unlimited Policy '{policyName}' was not applied to queue '{queueName}'.");
        }

        await brokerVerifier.ValidateDeliveryLimit(queueName);

        for (int i = 0; i < attempts; i++)
        {
            var queue = await managementClient.GetQueue(queueName);
            deliveryLimit = queue.GetDeliveryLimit();

            if (deliveryLimit == Queue.BigValueInsteadOfActuallyUnlimited && queue.AppliedPolicyName == policyName)
            {
                // Policy applied successfully
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(3));
        }

        Assert.Fail($"Policy '{policyName}' was not applied to queue '{queueName}'.");
    }

    [Test]
    public async Task ValidateDeliveryLimit_Should_Update_Old_Overeager_Policy()
    {
        var managementClient = new ManagementClient(connectionConfiguration);
        using var brokerVerifier = new BrokerVerifier(managementClient, BrokerRequirementChecks.None, true);
        await brokerVerifier.Initialize();

        if (brokerVerifier.BrokerVersion < BrokerVerifier.BrokerVersion4)
        {
            Assert.Ignore("Test not valid for broker versions before 4.0.0");
        }

        var queueName = nameof(ValidateDeliveryLimit_Should_Update_Old_Overeager_Policy);
        var policyName = $"nsb.{queueName}.delivery-limit";

        var oldQueueName = queueName[..^7];
        var oldPolicyName = $"nsb.{oldQueueName}.delivery-limit";

        var oldPolicy = new Policy
        {
            ApplyTo = PolicyTarget.QuorumQueues,
            Definition = new PolicyDefinition { DeliveryLimit = -1 },
            Pattern = oldQueueName,
            Priority = 0
        };

        await CreateQueue(queueName);
        await managementClient.CreatePolicy(oldPolicyName, oldPolicy);

        // It can take some time for updated policies to be applied, so we need to wait.
        // If this test is randomly failing, consider increasing the attempts
        var attempts = 20;

        int deliveryLimit = 0;

        for (int i = 0; i < attempts; i++)
        {
            var queue = await managementClient.GetQueue(queueName);
            deliveryLimit = queue.GetDeliveryLimit();

            if (deliveryLimit == -1 && queue.AppliedPolicyName == oldPolicyName)
            {
                // Policy applied successfully
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(3));
        }

        if (deliveryLimit != -1)
        {
            Assert.Fail($"Old Policy '{oldPolicyName}' was not applied to queue '{queueName}'.");
        }

        await brokerVerifier.ValidateDeliveryLimit(queueName);

        for (int i = 0; i < attempts; i++)
        {
            var queue = await managementClient.GetQueue(queueName);
            deliveryLimit = queue.GetDeliveryLimit();

            if (deliveryLimit == Queue.BigValueInsteadOfActuallyUnlimited && queue.AppliedPolicyName == policyName)
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
        using var brokerVerifier = new BrokerVerifier(managementClient, BrokerRequirementChecks.None, true);
        await brokerVerifier.Initialize();

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await brokerVerifier.ValidateDeliveryLimit(queueName));
        Assert.That(exception.Message, Does.Contain($"Cannot validate the delivery limit of the '{queueName}' queue because it does not exist."));
    }

    [Test]
    public async Task ValidateDeliveryLimit_Should_Pass_When_Called_For_Classic_Queue()
    {
        var queueName = nameof(ValidateDeliveryLimit_Should_Pass_When_Called_For_Classic_Queue);
        await CreateQueue(queueName, queueType: "classic");

        var managementClient = new ManagementClient(connectionConfiguration);
        using var brokerVerifier = new BrokerVerifier(managementClient, BrokerRequirementChecks.None, true);
        await brokerVerifier.Initialize();

        await brokerVerifier.ValidateDeliveryLimit(queueName);
    }

    [Test]
    public async Task ValidateDeliveryLimit_Should_Throw_When_Queue_Argument_Has_Delivery_Limit_Not_Set_To_Unlimited()
    {
        var queueName = nameof(ValidateDeliveryLimit_Should_Throw_When_Queue_Argument_Has_Delivery_Limit_Not_Set_To_Unlimited);
        var deliveryLimit = 5;
        await CreateQueue(queueName, deliveryLimit: deliveryLimit);

        var managementClient = new ManagementClient(connectionConfiguration);
        using var brokerVerifier = new BrokerVerifier(managementClient, BrokerRequirementChecks.None, true);
        await brokerVerifier.Initialize();

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await brokerVerifier.ValidateDeliveryLimit(queueName));
        Assert.That(exception.Message, Does.Contain($"The delivery limit for '{queueName}' is set to the non-default value of '{deliveryLimit}'. Remove any delivery limit settings from queue arguments, user policies or operator policies to correct this."));
    }

    [Test]
    public async Task ValidateDeliveryLimit_Should_Throw_When_A_Policy_On_Queue_Has_Delivery_Limit_Not_Set_To_Unlimited()
    {
        var queueName = nameof(ValidateDeliveryLimit_Should_Throw_When_A_Policy_On_Queue_Has_Delivery_Limit_Not_Set_To_Unlimited);
        await CreateQueue(queueName);

        var managementClient = new ManagementClient(connectionConfiguration);
        using var brokerVerifier = new BrokerVerifier(managementClient, BrokerRequirementChecks.None, true);
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

        // If this test appears flaky, the delay should be increased to give the broker more time to apply the oldPolicy before calling ValidateDeliveryLimit
        await Task.Delay(TimeSpan.FromSeconds(30));

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await brokerVerifier.ValidateDeliveryLimit(queueName));
        Assert.That(exception.Message, Does.Contain($"The delivery limit for '{queueName}' is set to the non-default value of '{deliveryLimit}'. Remove any delivery limit settings from queue arguments, user policies or operator policies to correct this."));
    }

    static async Task CreateQueue(string queueName, string queueType = "quorum", int? deliveryLimit = null)
    {
        using var connection = await connectionFactory.CreateAdministrationConnection().ConfigureAwait(false);
        using var channel = await connection.CreateChannelAsync().ConfigureAwait(false);

        var arguments = new Dictionary<string, object?> { { "x-queue-type", queueType } };

        if (deliveryLimit is not null)
        {
            arguments.Add("x-delivery-limit", deliveryLimit.Value);
        }

        _ = await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: arguments);
    }
}
