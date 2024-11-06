namespace NServiceBus.Transport.RabbitMQ.Administration.ManagementClient.Models;

/// <summary>
/// The types of queues supported by RabbitMQ
/// </summary>
/// <remarks>
/// Note that this is different to <see cref="NServiceBus.QueueType"/> which lists the types of queues supported by the NServiceBus transport 
/// and doesn't include the <c>Stream</c> value
/// </remarks>
enum QueueType
{
    Classic,
    Quorum,
    Stream
}
