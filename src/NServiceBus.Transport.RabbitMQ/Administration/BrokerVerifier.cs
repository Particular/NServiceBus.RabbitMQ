#nullable enable

namespace NServiceBus.Transport.RabbitMQ;

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Transport.RabbitMQ.ManagementApi;

class BrokerVerifier(ManagementClient managementClient, BrokerRequirementChecks disabledBrokerRequirementChecks, bool validateDeliveryLimits) : IDisposable
{
    static readonly Version MinimumSupportedBrokerVersion = Version.Parse("3.10.0");
    public static readonly Version BrokerVersion4 = Version.Parse("4.0.0");

    Version? brokerVersion;
    bool disposed;

    public Version BrokerVersion
    {
        get
        {
            if (brokerVersion == null)
            {
                throw new InvalidOperationException($"Need to call Initialize before accessing {nameof(BrokerVersion)} property.");
            }

            return brokerVersion;
        }
    }

    public async Task Initialize(CancellationToken cancellationToken = default)
    {
        //This needs to stay in sync with changes to BrokerRequirementChecks
        var all = BrokerRequirementChecks.Version310OrNewer | BrokerRequirementChecks.StreamsEnabled;

        if (disabledBrokerRequirementChecks == all && !validateDeliveryLimits)
        {
            return;
        }

        try
        {
            var overview = await managementClient.GetOverview(cancellationToken).ConfigureAwait(false);

            brokerVersion = RemovePrereleaseFromVersion(overview.BrokerVersion);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("There was a problem accessing the RabbitMQ management API while initializing the transport. See the inner exception for details.", ex);
        }

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

    public async Task VerifyRequirements(CancellationToken cancellationToken = default)
    {
        if ((disabledBrokerRequirementChecks & BrokerRequirementChecks.Version310OrNewer) == BrokerRequirementChecks.Version310OrNewer)
        {
            LogWarning("Verification of the minimum supported broker version has been disabled. The transport will not be able to ensure the delayed delivery infrastructure works properly.");
        }
        else
        {
            VerifyBrokerMinimumVersion();
        }

        if ((disabledBrokerRequirementChecks & BrokerRequirementChecks.StreamsEnabled) == BrokerRequirementChecks.StreamsEnabled)
        {
            LogWarning("Verification of the 'stream_queue' feature flag has been disabled. The transport will not be able to ensure the delayed delivery infrastructure works properly.");
        }
        else
        {
            await VerifyStreamEnabled(cancellationToken).ConfigureAwait(false);
        }

        void VerifyBrokerMinimumVersion()
        {
            if (BrokerVersion < MinimumSupportedBrokerVersion)
            {
                throw new InvalidOperationException($"An unsupported broker version was detected: {BrokerVersion}. The broker must be at least version {MinimumSupportedBrokerVersion}.");
            }
        }

        async Task VerifyStreamEnabled(CancellationToken cancellationToken)
        {
            bool streamsEnabled;

            try
            {
                var featureFlags = await managementClient.GetFeatureFlags(cancellationToken).ConfigureAwait(false);
                streamsEnabled = featureFlags.HasEnabledFeature(FeatureFlag.StreamQueue);
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException("There was a problem accessing the RabbitMQ management API to verify broker requirements. See the inner exception for details.", ex);
            }

            if (!streamsEnabled)
            {
                throw new InvalidOperationException("An unsupported broker configuration was detected. The 'stream_queue' feature flag needs to be enabled.");
            }
        }
    }

    public async Task ValidateDeliveryLimit(string queueName, CancellationToken cancellationToken = default)
    {
        if (!validateDeliveryLimits)
        {
            LogWarning("Validation of delivery limits has been disabled. The transport will not be able to ensure that messages are not lost after repeated retries.");
            return;
        }

        var queue = await GetQueueDetails(queueName, cancellationToken).ConfigureAwait(false);

        if (ShouldOverrideDeliveryLimit(queue))
        {
            await SetDeliveryLimitViaPolicy(queue, cancellationToken).ConfigureAwait(false);
        }
    }

    async Task<Queue> GetQueueDetails(string queueName, CancellationToken cancellationToken)
    {
        Queue? queue = null;
        var attempts = 20;

        for (int i = 0; i < attempts; i++)
        {
            try
            {
                queue = await managementClient.GetQueue(queueName, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException($"Cannot validate the delivery limit of the '{queueName}' queue because it does not exist.", ex);
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException($"There was a problem accessing the RabbitMQ management API to validate the delivery limit of the '{queueName}' queue. See the inner exception for details.", ex);
            }

            if (queue.EffectivePolicyDefinition is not null)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
        }

        return queue ?? throw new InvalidOperationException($"Cannot validate the delivery limit of the '{queueName}' queue because the queue details could not be retrieved from the RabbitMQ management API after multiple attempts.");
    }

    bool ShouldOverrideDeliveryLimit(Queue queue)
    {
        if (queue.QueueType is not QueueType.Quorum)
        {
            return false;
        }

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
            Priority = 0
        };

        try
        {
            await managementClient.CreatePolicy(policyName, policy, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"There was a problem accessing the RabbitMQ management API to create an unlimited delivery limit policy for the '{queue.Name}' queue. See the inner exception for details.", ex);
        }
    }

    static void LogWarning(string message)
    {
#if !COMMANDLINE
        var logger = Logging.LogManager.GetLogger(typeof(BrokerVerifier));
        logger.Warn(message);
#endif
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                managementClient.Dispose();
            }

            disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
