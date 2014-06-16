namespace NServiceBus.Transports.RabbitMQ
{
    using global::RabbitMQ.Client;

    interface IChannelProvider
    {
        bool TryGetPublishChannel(out IModel channel);

        ConfirmsAwareChannel GetNewPublishChannel();
    }
}