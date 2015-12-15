namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using global::RabbitMQ.Client;
    using NServiceBus.Extensibility;

    class ChannelProvider:IChannelProvider
    {
        private readonly IManageRabbitMqConnections connectionManager;
        private readonly bool usePublisherConfirms;
        private readonly TimeSpan maxWaitTimeForConfirms;

        public ChannelProvider(IManageRabbitMqConnections connectionManager, bool usePublisherConfirms, TimeSpan maxWaitTimeForConfirms)
        {
            this.connectionManager = connectionManager;
            this.usePublisherConfirms = usePublisherConfirms;
            this.maxWaitTimeForConfirms = maxWaitTimeForConfirms;
        }

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
           return new ConfirmsAwareChannel(connectionManager.GetPublishConnection(), usePublisherConfirms, maxWaitTimeForConfirms);
        }
    }
}