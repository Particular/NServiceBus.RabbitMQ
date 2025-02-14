namespace NServiceBus.Transport.RabbitMQ.ManagementApi;

enum PolicyTarget
{
    All,
    Queues,
    ClassicQueues,
    QuorumQueues,
    Streams,
    Exchanges,
}
