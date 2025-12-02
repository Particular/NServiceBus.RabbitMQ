namespace NServiceBus.Transport.RabbitMQ.ManagementApi;

// Note that this is different to `NServiceBus.QueueType` which lists the types of queues supported
// by the NServiceBus transport, which doesn't include the `Stream` value
enum QueueType
{
    Unknown,
    Classic,
    Quorum,
    Stream,
    MqttQos0
}
