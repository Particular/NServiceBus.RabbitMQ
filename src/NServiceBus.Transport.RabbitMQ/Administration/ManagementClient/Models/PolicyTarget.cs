namespace NServiceBus.Transport.RabbitMQ.ManagementClient;

enum PolicyTarget
{
    All,
    Queues,
    ClassicQueues,
    QuorumQueues,
    Streams,
    Exchanges,
}
