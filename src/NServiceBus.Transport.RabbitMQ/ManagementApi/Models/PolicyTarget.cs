namespace NServiceBus.Transport.RabbitMQ.ManagementApi.Models;

enum PolicyTarget
{
    All,
    Queues,
    ClassicQueues,
    QuorumQueues,
    Streams,
    Exchanges,
}
