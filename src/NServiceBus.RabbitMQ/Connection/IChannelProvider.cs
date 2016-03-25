namespace NServiceBus.Transports.RabbitMQ
{
    interface IChannelProvider
    {
        ConfirmsAwareChannel GetNewPublishChannel();
    }
}