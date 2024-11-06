﻿#nullable enable

namespace NServiceBus.Transport.RabbitMQ.Administration;

using System;
using System.Threading;
using System.Threading.Tasks;
using ManagementClient;
using ManagementClient.Models;
using NServiceBus.Logging;
using Polly;

class BrokerVerifier(ConnectionFactory connectionFactory, IManagementClientFactory managementClientFactory) : IBrokerVerifier
{
    static readonly ILog Logger = LogManager.GetLogger(typeof(BrokerVerifier));
    static readonly Version MinimumSupportedRabbitMqVersion = Version.Parse("3.10.0");

    readonly IManagementClient managementClient = managementClientFactory.CreateManagementClient();

    Overview? overview;
    Version? brokerVersion;

    public async Task Initialize(CancellationToken cancellationToken = default)
    {
        var response = await managementClient.GetOverview(cancellationToken).ConfigureAwait(false);
        if (response.HasValue)
        {
            overview = response.Value;
            brokerVersion = overview.RabbitMqVersion;
        }
        else
        {
            // TODO: Need logic/config settings for determining which action to take if management API unavailable, e.g. should we throw an exception to refuse to start, or just log a warning
            Logger.WarnFormat("Could not access RabbitMQ Management API. ({0}: {1})", response.StatusCode, response.Reason);

            using var connection = await connectionFactory.CreateAdministrationConnection(cancellationToken).ConfigureAwait(false);
            brokerVersion = connection.GetBrokerVersion();
        }
    }

    bool HasManagementClientAccess => overview != null;

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
        if (HasManagementClientAccess)
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
        if (!HasManagementClientAccess)
        {
            return;
        }

        var queue = await GetFullQueueDetails(queueName, cancellationToken).ConfigureAwait(false);
        if (queue is null)
        {
            // TODO: Need logic/config settings for determining which action to take, e.g. should we throw an exception to refuse to start, or just log a warning
            Logger.WarnFormat("Could not retrieve full queue details for {0}.", queueName);
            return;
        }

        if (queue.DeliveryLimit == -1)
        {
            return;
        }

        if (queue.Arguments.DeliveryLimit.HasValue &&
            queue.Arguments.DeliveryLimit != -1)
        {
            // TODO: Need logic/config settings for determining which action to take, e.g. should we throw an exception to refuse to start, or just log a warning
            Logger.WarnFormat("The delivery limit for {0} is set to {1} by a queue argument. This can interfere with the transport's retry implementation",
                queue.Name, queue.Arguments.DeliveryLimit);
            return;
        }

        if (queue.EffectivePolicyDefinition!.DeliveryLimit.HasValue &&
            queue.EffectivePolicyDefinition.DeliveryLimit != -1)
        {
            // TODO: Need logic/config settings for determining which action to take, e.g. should we throw an exception to refuse to start, or just log a warning
            Logger.WarnFormat("The RabbitMQ policy {2} is setting delivery limit to {1} for {0}.",
                queue.Name, queue.EffectivePolicyDefinition.DeliveryLimit, queue.AppliedPolicyName);
            return;
        }

        await SetDeliveryLimitViaPolicy(queue, cancellationToken).ConfigureAwait(false);
    }

    async Task<Queue?> GetFullQueueDetails(string queueName, CancellationToken cancellationToken)
    {
        var retryPolicy = Polly.Policy
            .HandleResult<Response<Queue?>>(response => response.Value?.EffectivePolicyDefinition is null)
            .WaitAndRetryAsync(
                5,
                attempt => TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt - 1)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    if (outcome.Exception is not null)
                    {
                        Logger.Error($"Failed to get {queueName} queue", outcome.Exception);
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

    async Task SetDeliveryLimitViaPolicy(Queue queue, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(queue.AppliedPolicyName))
        {
            // TODO: Need logic/config settings for determining which action to take, e.g. should we throw an exception to refuse to start, or just log a warning
            Logger.WarnFormat("The {0} queue already has an associated policy.", queue.Name, queue.AppliedPolicyName);
            return;
        }

        if (BrokerVersion.Major < 4)
        {
            // TODO: Need logic/config settings for determining which action to take, e.g. should we throw an exception to refuse to start, or just log a warning
            Logger.WarnFormat("Cannot override delivery limit on the {0} queue by policy in RabbitMQ versions prior to 4.", queue.Name);
            return;
        }

        var policy = new ManagementClient.Models.Policy
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
