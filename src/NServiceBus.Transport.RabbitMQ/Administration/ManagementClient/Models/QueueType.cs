namespace NServiceBus.Transport.RabbitMQ.ManagementClient;

// Note that this is different to `NServiceBus.QueueType` which lists the types of queues supported
// by the NServiceBus transport, which doesn't include the `Stream` value
enum QueueType
{
    Classic,
    Quorum,
    Stream
}
