namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using global::RabbitMQ.Client;
    using NServiceBus.Extensibility;

    class ChannelProvider:IChannelProvider
    {
        public IManageRabbitMqConnections ConnectionManager { get; set; }

        public bool UsePublisherConfirms { get; set; }

        public TimeSpan MaxWaitTimeForConfirms { get; set; }

        bool IChannelProvider.TryGetPublishChannel(ContextBag context, out IModel channel)
        {
            OpenPublishChannelBehavior.RabbitMq_PublishChannel publishChannel;

            if (!context.TryGet(out publishChannel))
            {
                channel = null;
                return false;
            }

            channel = publishChannel.LazyChannel.Value.Channel;

            return true;
        }

        public ConfirmsAwareChannel GetNewPublishChannel()
        {
           return new ConfirmsAwareChannel(ConnectionManager.GetPublishConnection(), UsePublisherConfirms,MaxWaitTimeForConfirms);
        }
    }
}