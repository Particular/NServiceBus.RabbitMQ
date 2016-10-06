namespace NServiceBus.Transport.RabbitMQ
{
    interface IChannelProvider
    {
        ConfirmsAwareChannel GetPublishChannel();

        void ReturnPublishChannel(ConfirmsAwareChannel channel);
    }
}