#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementClient;

class ManagementClientFactory(ConnectionConfiguration connectionConfiguration) : IManagementClientFactory
{
    public IManagementClient CreateManagementClient() => new ManagementClient(connectionConfiguration);
}
