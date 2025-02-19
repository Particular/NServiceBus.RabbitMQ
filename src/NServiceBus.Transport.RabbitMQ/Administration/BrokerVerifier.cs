#nullable enable

namespace NServiceBus.Transport.RabbitMQ;

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Logging;
using NServiceBus.Transport.RabbitMQ.ManagementApi;

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
            throw new InvalidOperationException($"Could not access the RabbitMQ Management API. ({response.StatusCode}: {response.Reason})");
        }

        brokerVersion = RemovePrereleaseFromVersion(response.Value.BrokerVersion);

        static Version RemovePrereleaseFromVersion(string version)
        {
            var index = version.IndexOf('-');

            if (index is -1)
            {
                index = version.Length;
            }

            var versionSpan = version.AsSpan()[..index];

            return Version.Parse(versionSpan);
        }
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

        var queue = await GetQueueDetails(queueName, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Could not get queue details for '{queueName}'.");

        if (ShouldOverrideDeliveryLimit(queue))
        {
            await SetDeliveryLimitViaPolicy(queue, cancellationToken).ConfigureAwait(false);
        }
    }

    bool ShouldOverrideDeliveryLimit(Queue queue)
    {
        var limit = queue.GetDeliveryLimit();

        if (limit == -1)
        {
            return false;
        }

        if (queue.Arguments.DeliveryLimit.HasValue || (queue.EffectivePolicyDefinition?.DeliveryLimit.HasValue ?? false))
        {
            throw new InvalidOperationException($"The delivery limit for '{queue.Name}' is set to the non-default value of '{limit}'. Remove any delivery limit settings from queue arguments, user policies or operator policies to correct this.");
        }

        return true;
    }

    async Task<Queue?> GetQueueDetails(string queueName, CancellationToken cancellationToken)
    {
        Queue? queue = null;
        var attempts = 20;

        for (int i = 0; i < attempts; i++)
        {
            var response = await managementClient.GetQueue(queueName, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                break;
            }

            if (response.Value?.EffectivePolicyDefinition is not null)
            {
                queue = response.Value;
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
        }

        return queue;
    }

    async Task SetDeliveryLimitViaPolicy(Queue queue, CancellationToken cancellationToken)
    {
        if (BrokerVersion < BrokerVersion4)
        {
            throw new InvalidOperationException($"Cannot create unlimited delivery limit policies in RabbitMQ versions prior to 4.0. The version is: {brokerVersion}.");
        }

        if (!string.IsNullOrEmpty(queue.AppliedPolicyName))
        {
            throw new InvalidOperationException($"An unlimited delivery limit policy cannot be applied to the '{queue.Name}' queue because it already has a '{queue.AppliedPolicyName}' policy applied.");
        }

        var policyName = $"nsb.{queue.Name}.delivery-limit";

        var policy = new Policy
        {
            ApplyTo = PolicyTarget.QuorumQueues,
            Definition = new PolicyDefinition { DeliveryLimit = -1 },
            Pattern = queue.Name,
            Priority = 100
        };

        await managementClient.CreatePolicy(policyName, policy, cancellationToken).ConfigureAwait(false);
    }
}
