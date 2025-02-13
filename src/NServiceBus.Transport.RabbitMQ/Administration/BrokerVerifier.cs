#nullable enable

namespace NServiceBus.Transport.RabbitMQ;

using System;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Logging;
using NServiceBus.Transport.RabbitMQ.ManagementApi;
using Polly;

class BrokerVerifier(ManagementClient managementClient, bool validateDeliveryLimits)
{
    static readonly ILog Logger = LogManager.GetLogger(typeof(BrokerVerifier));

    static readonly Version MinimumSupportedBrokerVersion = Version.Parse("3.10.0");
    public static readonly Version BrokerVersion4 = Version.Parse("4.0.0");

    Version? brokerVersion;

    public async Task Initialize(CancellationToken cancellationToken = default)
    {
        var response = await managementClient.GetOverview(cancellationToken).ConfigureAwait(false);

        if (!response.HasValue)
        {
            throw new InvalidOperationException($"Could not access RabbitMQ Management API. ({response.StatusCode}: {response.Reason})");
        }

        brokerVersion = response.Value.BrokerVersion;
    }

    public Version BrokerVersion
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
        if (BrokerVersion < MinimumSupportedBrokerVersion)
        {
            throw new Exception($"An unsupported broker version was detected: {BrokerVersion}. The broker must be at least version {MinimumSupportedBrokerVersion}.");
        }

        bool streamsEnabled;

        var response = await managementClient.GetFeatureFlags(cancellationToken).ConfigureAwait(false);
        streamsEnabled = response.HasValue && response.Value.HasEnabledFeature(FeatureFlags.StreamQueue);

        if (!streamsEnabled)
        {
            throw new Exception("An unsupported broker configuration was detected. The 'stream_queue' feature flag needs to be enabled.");
        }
    }

    public async Task ValidateDeliveryLimit(string queueName, CancellationToken cancellationToken = default)
    {
        if (!validateDeliveryLimits)
        {
            Logger.Warn("Validation of delivery limits has been disabled. The transport will not be able to ensure that messages are not lost after repeated retries.");

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
        var limit = queue.GetDeliveryLimit();

        if (limit == -1)
        {
            return false;
        }

        if (queue.Arguments.DeliveryLimit.HasValue || queue.EffectivePolicyDefinition!.DeliveryLimit.HasValue)
        {
            throw new InvalidOperationException($"The delivery limit for {queue.Name} is set to the non-default value of '{queue.Arguments.DeliveryLimit}'.");
        }

        return true;
    }

    async Task<Queue?> GetFullQueueDetails(ManagementClient managementClient, string queueName, CancellationToken cancellationToken)
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

    static async Task SetDeliveryLimitViaPolicy(ManagementClient managementClient, Queue queue, Version brokerVersion, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(queue.AppliedPolicyName))
        {
            throw new InvalidOperationException($"The {queue.Name} queue already has the '{queue.AppliedPolicyName}' policy applied.");
        }

        if (brokerVersion < BrokerVersion4)
        {
            throw new InvalidOperationException($"Cannot override delivery limit on the {queue.Name} queue by policy in RabbitMQ versions prior to 4. Version is {brokerVersion}.");
        }

        var policyName = $"nsb.{queue.Name}.delivery-limit";

        var policy = new ManagementApi.Policy
        {
            ApplyTo = PolicyTarget.QuorumQueues,
            Definition = new PolicyDefinition { DeliveryLimit = -1 },
            Pattern = queue.Name,
            Priority = 100
        };

        await managementClient.CreatePolicy(policyName, policy, cancellationToken).ConfigureAwait(false);
    }
}
