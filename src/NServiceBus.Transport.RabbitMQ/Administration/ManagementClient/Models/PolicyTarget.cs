namespace NServiceBus.Transport.RabbitMQ.Administration.ManagementClient.Models;

enum PolicyTarget
{
    All,
    Queues,
    ClassicQueues,
    QuorumQueues,
    Streams,
    Exchanges,
}
