#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementClient;

interface IManagementClientFactory
{
    ManagementClient CreateManagementClient();
}
