namespace NServiceBus.Transports.RabbitMQ
{
    using global::RabbitMQ.Client;
    using NServiceBus.Extensibility;

    interface IChannelProvider
    {
        bool TryGetPublishChannel(ContextBag context, out IModel channel);

        ConfirmsAwareChannel GetNewPublishChannel();
    }
}