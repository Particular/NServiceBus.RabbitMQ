#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementClient;

using System.Threading;
using System.Threading.Tasks;

interface IManagementClient
{
    Task<Response<Queue?>> GetQueue(string queueName, CancellationToken cancellationToken = default);

    Task<Response<Overview?>> GetOverview(CancellationToken cancellationToken = default);

    Task<Response<FeatureFlagList?>> GetFeatureFlags(CancellationToken cancellationToken = default);

    Task CreatePolicy(Policy policy, CancellationToken cancellationToken = default);
}
