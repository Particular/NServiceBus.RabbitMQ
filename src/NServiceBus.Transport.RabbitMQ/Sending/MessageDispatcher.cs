namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;

    class MessageDispatcher : IMessageDispatcher
    {
        readonly ChannelProvider channelProvider;
        readonly Action<IOutgoingTransportOperation, IBasicProperties> messageCustomization;
        readonly bool supportsDelayedDelivery;

        public MessageDispatcher(
            ChannelProvider channelProvider,
            Action<IOutgoingTransportOperation, IBasicProperties> messageCustomization,
            bool supportsDelayedDelivery
        )
        {
            this.channelProvider = channelProvider;
            this.messageCustomization = messageCustomization ?? (static (_, _) => { });
            this.supportsDelayedDelivery = supportsDelayedDelivery;
        }

        public async Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, CancellationToken cancellationToken = default)
        {
            var channel = await channelProvider.GetPublishChannel(cancellationToken).ConfigureAwait(false);

            try
            {
                var unicastTransportOperations = outgoingMessages.UnicastTransportOperations;
                var multicastTransportOperations = outgoingMessages.MulticastTransportOperations;

                var tasks = new List<Task>(unicastTransportOperations.Count + multicastTransportOperations.Count);

                foreach (var operation in unicastTransportOperations)
                {
                    tasks.Add(SendMessage(operation, channel, cancellationToken));
                }

                foreach (var operation in multicastTransportOperations)
                {
                    tasks.Add(PublishMessage(operation, channel, cancellationToken));
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            finally
            {
                channelProvider.ReturnPublishChannel(channel);
            }
        }

        Task SendMessage(UnicastTransportOperation transportOperation, ConfirmsAwareChannel channel, CancellationToken cancellationToken)
        {
            ThrowIfDelayedDeliveryIsDisabledAndMessageIsDelayed(transportOperation);

            var message = transportOperation.Message;

            var properties = new BasicProperties();
            properties.Fill(message, transportOperation.Properties);
            messageCustomization(transportOperation, properties);

            return channel.SendMessage(transportOperation.Destination, message, properties, cancellationToken);
        }

        Task PublishMessage(MulticastTransportOperation transportOperation, ConfirmsAwareChannel channel, CancellationToken cancellationToken)
        {
            ThrowIfDelayedDeliveryIsDisabledAndMessageIsDelayed(transportOperation);

            var message = transportOperation.Message;

            var properties = new BasicProperties();
            properties.Fill(message, transportOperation.Properties);
            messageCustomization(transportOperation, properties);

            return channel.PublishMessage(transportOperation.MessageType, message, properties, cancellationToken);
        }

        void ThrowIfDelayedDeliveryIsDisabledAndMessageIsDelayed(IOutgoingTransportOperation transportOperation)
        {
            if (supportsDelayedDelivery)
            {
                return;
            }

            if (transportOperation.Properties.DelayDeliveryWith != null || transportOperation.Properties.DoNotDeliverBefore != null)
            {
                ThrowDelayedDeliveryDisabled();
            }
        }

        [DoesNotReturn]
        static void ThrowDelayedDeliveryDisabled() => throw new Exception("Delayed delivery has been disabled in the transport settings.");
    }
}
