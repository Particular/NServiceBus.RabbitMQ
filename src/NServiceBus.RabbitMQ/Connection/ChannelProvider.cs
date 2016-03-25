namespace NServiceBus.Transports.RabbitMQ
{
    using System;

    class ChannelProvider : IChannelProvider
    {
        public ChannelProvider(ConnectionManager connectionManager, bool usePublisherConfirms, TimeSpan maxWaitTimeForConfirms)
        {
            this.connectionManager = connectionManager;
            this.usePublisherConfirms = usePublisherConfirms;
            this.maxWaitTimeForConfirms = maxWaitTimeForConfirms;
        }

        public ConfirmsAwareChannel GetNewPublishChannel()
        {
            return new ConfirmsAwareChannel(connectionManager.GetPublishConnection(), usePublisherConfirms, maxWaitTimeForConfirms);
        }

        ConnectionManager connectionManager;
        bool usePublisherConfirms;
        TimeSpan maxWaitTimeForConfirms;
    }
}