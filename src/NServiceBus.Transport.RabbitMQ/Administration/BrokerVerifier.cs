#nullable enable

namespace NServiceBus.Transport.RabbitMQ;

using System;
using System.Threading;
using System.Threading.Tasks;
using ManagementClientClass = ManagementClient.ManagementClient;
using NServiceBus.Logging;
using NServiceBus.Transport.RabbitMQ.ManagementClient;
using Polly;

class BrokerVerifier(ConnectionFactory connectionFactory, bool managementClientAvailable, ManagementClientClass managementClient, ILog? logger = null)
{
    readonly ILog Logger = logger ?? LogManager.GetLogger(typeof(BrokerVerifier));
    static readonly Version MinimumSupportedRabbitMqVersion = Version.Parse("3.10.0");
    static readonly Version RabbitMqVersion4 = Version.Parse("4.0.0");

    Version? brokerVersion;

    public async Task Initialize(CancellationToken cancellationToken = default)
    {
        if (managementClientAvailable)
        {
            var response = await managementClient.GetOverview(cancellationToken).ConfigureAwait(false);
            if (response.HasValue)
            {
                brokerVersion = response.Value.RabbitMqVersion;
                return;
            }

            throw new InvalidOperationException($"Could not access RabbitMQ Management API. ({response.StatusCode}: {response.Reason})");
        }

        using var connection = await connectionFactory.CreateAdministrationConnection(cancellationToken).ConfigureAwait(false);
        brokerVersion = connection.GetBrokerVersion();

        if (brokerVersion >= RabbitMqVersion4)
        {
            Logger.Warn("Use of RabbitMQ Management API has been disabled." +
                "The transport will not be able to override the default delivery limit on each queue " +
                "which is necessary in order to guarantee that messages are not lost after repeated retries.");
        }
    }

    Version BrokerVersion
    {
        get
        {
            if (brokerVersion == null)
            {
                throw new InvalidOperationException($"Need to call Initialize before accessing {nameof(BrokerVersion)} property");
            }

            return brokerVersion;
        }
    }

    public async Task VerifyRequirements(CancellationToken cancellationToken = default)
    {
        if (BrokerVersion < MinimumSupportedRabbitMqVersion)
        {
            throw new Exception($"An unsupported broker version was detected: {BrokerVersion}. The broker must be at least version {MinimumSupportedRabbitMqVersion}.");
        }

        bool streamsEnabled;
        if (managementClientAvailable)
        {
            var response = await managementClient.GetFeatureFlags(cancellationToken).ConfigureAwait(false);
            streamsEnabled = response.HasValue && response.Value.HasEnabledFeature(FeatureFlags.StreamQueue);
        }
        else
        {
            var connection = await connectionFactory.CreateAdministrationConnection(cancellationToken).ConfigureAwait(false);
            streamsEnabled = await connection.TryCreateStream(cancellationToken).ConfigureAwait(false);
        }

        if (!streamsEnabled)
        {
            throw new Exception("An unsupported broker configuration was detected. The 'stream_queue' feature flag needs to be enabled.");
        }
    }

    public async Task ValidateDeliveryLimit(string queueName, CancellationToken cancellationToken = default)
    {
        if (!managementClientAvailable)
        {
            return;
        }

        var queue = await GetFullQueueDetails(managementClient, queueName, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Could not retrieve full queue details for {queueName}.");

        if (ShouldOverrideDeliveryLimit(queue))
        {
            await SetDeliveryLimitViaPolicy(managementClient, queue, BrokerVersion, cancellationToken).ConfigureAwait(false);
        }
    }

    bool ShouldOverrideDeliveryLimit(Queue queue)
    {
        if (BrokerVersion < RabbitMqVersion4)
        {
            return false;
        }

        if (queue.DeliveryLimit == -1)
        {
            return false;
        }

        if (queue.Arguments.DeliveryLimit.HasValue && queue.Arguments.DeliveryLimit != -1)
        {
            throw new InvalidOperationException($"The delivery limit for {queue.Name} is set to {queue.Arguments.DeliveryLimit} by a queue argument. " +
                "This can interfere with the transport's retry implementation");
        }

        if (queue.EffectivePolicyDefinition!.DeliveryLimit.HasValue && queue.EffectivePolicyDefinition.DeliveryLimit != -1)
        {
            throw new InvalidOperationException($"The RabbitMQ policy {queue.AppliedPolicyName} " +
                $"is setting delivery limit to {queue.EffectivePolicyDefinition.DeliveryLimit} for {queue.Name}.");
        }

        return true;
    }

    async Task<Queue?> GetFullQueueDetails(ManagementClientClass managementClient, string queueName, CancellationToken cancellationToken)
    {
        var retryPolicy = Polly.Policy
            .HandleResult<Response<Queue?>>(response => response.Value?.EffectivePolicyDefinition is null)
            .WaitAndRetryAsync(
                5,
                attempt => TimeSpan.FromMilliseconds(3000 * Math.Pow(2, attempt - 1)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    if (outcome.Exception is not null)
                    {
                        Logger.Error($"Failed to get {queueName} queue - Attempt #{retryCount}.", outcome.Exception);
                    }
                    else if (!outcome.Result.HasValue)
                    {
                        var response = outcome.Result;
                        Logger.WarnFormat("Could not get queue details for {0} - Attempt #{1}. ({2}: {3})", queueName, retryCount, response.StatusCode, response.Reason);
                    }
                    else
                    {
                        var response = outcome.Result;
                        Logger.WarnFormat("Did not receive full queue details for {0} - Attempt #{1})", queueName, retryCount);
                    }
                });

        var response = await retryPolicy.ExecuteAsync(() => managementClient.GetQueue(queueName, cancellationToken)).ConfigureAwait(false);

        return response?.Value?.EffectivePolicyDefinition is not null ? response.Value : null;
    }

    static async Task SetDeliveryLimitViaPolicy(ManagementClientClass managementClient, Queue queue, Version brokerVersion, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(queue.AppliedPolicyName))
        {
            throw new InvalidOperationException($"The {queue.Name} queue already has the '{queue.AppliedPolicyName}' policy applied.");
        }

        if (brokerVersion < RabbitMqVersion4)
        {
            throw new InvalidOperationException($"Cannot override delivery limit on the {queue.Name} queue by policy in RabbitMQ versions prior to 4. Version is {brokerVersion}.");
        }

        var policy = new ManagementClient.Policy
        {
            Name = $"nsb.{queue.Name}.delivery-limit",
            ApplyTo = PolicyTarget.QuorumQueues,
            Definition = new PolicyDefinition { DeliveryLimit = -1 },
            Pattern = queue.Name,
            Priority = 100
        };

        await managementClient.CreatePolicy(policy, cancellationToken).ConfigureAwait(false);
    }
}
