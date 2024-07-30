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
        readonly bool supportsDelayedDelivery;

        public MessageDispatcher(ChannelProvider channelProvider, bool supportsDelayedDelivery)
        {
            this.channelProvider = channelProvider;
            this.supportsDelayedDelivery = supportsDelayedDelivery;
        }

        public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, CancellationToken cancellationToken = default)
        {
            var channel = channelProvider.GetPublishChannel();

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

                channelProvider.ReturnPublishChannel(channel);

                return tasks.Count == 1 ? tasks[0] : Task.WhenAll(tasks);
            }
#pragma warning disable PS0019 // When catching System.Exception, cancellation needs to be properly accounted for - justification:
            // the same action is appropriate when an operation was canceled
            catch
#pragma warning restore PS0019 // When catching System.Exception, cancellation needs to be properly accounted for
            {
                channel.Dispose();
                throw;
            }
        }

        Task SendMessage(UnicastTransportOperation transportOperation, ConfirmsAwareChannel channel, CancellationToken cancellationToken)
        {
            ThrowIfDelayedDeliveryIsDisabledAndMessageIsDelayed(transportOperation);

            var message = transportOperation.Message;

            var properties = new BasicProperties();
            properties.Fill(message, transportOperation.Properties);

            return channel.SendMessage(transportOperation.Destination, message, properties, cancellationToken);
        }

        Task PublishMessage(MulticastTransportOperation transportOperation, ConfirmsAwareChannel channel, CancellationToken cancellationToken)
        {
            ThrowIfDelayedDeliveryIsDisabledAndMessageIsDelayed(transportOperation);

            var message = transportOperation.Message;

            var properties = new BasicProperties();
            properties.Fill(message, transportOperation.Properties);

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
