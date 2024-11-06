#nullable enable

namespace NServiceBus.Transport.RabbitMQ.Administration.ManagementClient;

interface IManagementClientFactory
{
    IManagementClient CreateManagementClient();
}
