#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;

using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Transport.RabbitMQ.ManagementApi.Models;

interface IManagementApi
{
    Task<Queue?> GetQueue(string queueName, CancellationToken cancellationToken = default);
}
