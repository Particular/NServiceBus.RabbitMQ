#nullable enable

namespace NServiceBus.Transport.RabbitMQ.Administration;

using System.Threading.Tasks;
using System.Threading;

interface IBrokerVerifier
{
    Task Initialize(CancellationToken cancellationToken = default);

    Task ValidateDeliveryLimit(string queueName, CancellationToken cancellationToken = default);
}
