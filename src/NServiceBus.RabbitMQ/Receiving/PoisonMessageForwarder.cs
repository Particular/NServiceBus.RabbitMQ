namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client.Events;
    using Logging;

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

        public Task ForwardPoisonMessageToErrorQueue(BasicDeliverEventArgs message, Exception ex, string errorQueue)
        {
            var error = $"Poison message detected with deliveryTag '{message.DeliveryTag}'. Message will be moved to '{errorQueue}'.";
            Logger.Error(error, ex);

            var channel = channelProvider.GetPublishChannel();

            try
            {
                return channel.RawSendInCaseOfFailure(routingTopology.RawSendInCaseOfFailure, errorQueue, message.Body, message.BasicProperties);
            }
            catch (Exception ex2)
            {
                Logger.Error($"Poison message failed to be moved to '{errorQueue}'.", ex2);
                throw;
            }
            finally
            {
                channelProvider.ReturnPublishChannel(channel);
            }
        }
    }
}
