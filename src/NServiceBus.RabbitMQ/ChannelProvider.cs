namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using global::RabbitMQ.Client;
    using Pipeline;

    class ChannelProvider:IChannelProvider
    {
        public PipelineExecutor PipelineExecutor { get; set; }

        public IManageRabbitMqConnections ConnectionManager { get; set; }

        public bool UsePublisherConfirms { get; set; }

        public TimeSpan MaxWaitTimeForConfirms { get; set; }


        bool IChannelProvider.TryGetPublishChannel(out IModel channel)
        {
            Lazy<ConfirmsAwareChannel> lazyChannel;

            if (!PipelineExecutor.CurrentContext.TryGet("RabbitMq.PublishChannel", out lazyChannel))
            {
                channel = null;
                return false;
            }

            channel = lazyChannel.Value.Channel;

            return true;
        }

        public ConfirmsAwareChannel GetNewPublishChannel()
        {
           return new ConfirmsAwareChannel(ConnectionManager.GetPublishConnection(),UsePublisherConfirms,MaxWaitTimeForConfirms);
        }
    }
}