namespace NServiceBus.Transports.RabbitMQ
{
    using System;

    class ChannelProvider:IChannelProvider
    {
        public ChannelProvider(IManageRabbitMqConnections connectionManager, bool usePublisherConfirms, TimeSpan maxWaitTimeForConfirms)
        {
            this.connectionManager = connectionManager;
            this.usePublisherConfirms = usePublisherConfirms;
            this.maxWaitTimeForConfirms = maxWaitTimeForConfirms;
        }
        
        public ConfirmsAwareChannel GetNewPublishChannel()
        {
           return new ConfirmsAwareChannel(connectionManager.GetPublishConnection(), usePublisherConfirms, maxWaitTimeForConfirms);
        }

        IManageRabbitMqConnections connectionManager;
        bool usePublisherConfirms;
        TimeSpan maxWaitTimeForConfirms;
    }
}