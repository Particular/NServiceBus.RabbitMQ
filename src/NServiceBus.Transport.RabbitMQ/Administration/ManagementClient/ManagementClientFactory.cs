#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementClient;

class ManagementClientFactory(ConnectionConfiguration connectionConfiguration) : IManagementClientFactory
{
    public ManagementClient CreateManagementClient() => new ManagementClient(connectionConfiguration);
}
