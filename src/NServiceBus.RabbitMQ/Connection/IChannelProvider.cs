namespace NServiceBus.Transport.RabbitMQ
{
    interface IChannelProvider
    {
        ConfirmsAwareChannel GetNewPublishChannel();
    }
}