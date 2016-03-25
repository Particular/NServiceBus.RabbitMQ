namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using global::RabbitMQ.Client.Events;
    using NServiceBus.Logging;
    using NServiceBus.Transports.RabbitMQ.Routing;

    class PoisonMessageForwarder
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(PoisonMessageForwarder));

        readonly IChannelProvider channelProvider;
        readonly IRoutingTopology routingTopology;

        public PoisonMessageForwarder(IChannelProvider channelProvider, IRoutingTopology routingTopology)
        {
            this.channelProvider = channelProvider;
            this.routingTopology = routingTopology;
        }

        public void ForwardPoisonMessageToErrorQueue(BasicDeliverEventArgs message, Exception ex, string errorQueue)
        {
            var error = $"Poison message detected with deliveryTag '{message.DeliveryTag}'. Message will be moved to '{errorQueue}'.";
            Logger.Error(error, ex);

            try
            {
                using (var errorChannel = channelProvider.GetNewPublishChannel())
                {
                    routingTopology.RawSendInCaseOfFailure(errorChannel.Channel, errorQueue, message.Body, message.BasicProperties);
                }
            }
            catch (Exception ex2)
            {
                Logger.Error($"Poison message failed to be moved to '{errorQueue}'.", ex2);
                throw;
            }
        }
    }
}
