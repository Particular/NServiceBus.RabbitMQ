namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Extensibility;

    class MessageDispatcher : IDispatchMessages
    {
        readonly IChannelProvider channelProvider;
        static readonly string PublishIntent = MessageIntentEnum.Publish.ToString();

        public MessageDispatcher(IChannelProvider channelProvider)
        {
            this.channelProvider = channelProvider;
        }

        public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, ContextBag context)
        {
            var channel = channelProvider.GetPublishChannel();

            try
            {
                var unicastTransportOperations = outgoingMessages.UnicastTransportOperations;
                var multicastTransportOperations = outgoingMessages.MulticastTransportOperations;

                var tasks = new List<Task>(unicastTransportOperations.Count + multicastTransportOperations.Count);
                foreach (var operation in unicastTransportOperations)
                {
                    SendOrPublish(operation, tasks, channel);
                }

                foreach (var operation in multicastTransportOperations)
                {
                    SendOrPublish(operation, tasks, channel);
                }

                return tasks.Count == 1 ? tasks[0] : Task.WhenAll(tasks);
            }
            finally
            {
                channelProvider.ReturnPublishChannel(channel);
            }
        }

        void SendOrPublish(IOutgoingTransportOperation operation, List<Task> tasks, ConfirmsAwareChannel channel)
        {
            var intentHeader = operation.Message.Headers[Headers.MessageIntent];
            if (intentHeader.Equals(PublishIntent, StringComparison.Ordinal))
            {
                tasks.Add(PublishMessage(operation, channel));
            }
            else
            {
                tasks.Add(SendMessage(operation, channel));
            }
        }

        static Task SendMessage(IOutgoingTransportOperation transportOperation, ConfirmsAwareChannel channel)
        {
            var message = transportOperation.Message;

            var properties = channel.CreateBasicProperties();
            properties.Fill(message, transportOperation.DeliveryConstraints);

            return channel.SendMessage(transportOperation, properties);
        }

        static Task PublishMessage(IOutgoingTransportOperation transportOperation, ConfirmsAwareChannel channel)
        {
            var message = transportOperation.Message;

            var properties = channel.CreateBasicProperties();
            properties.Fill(message, transportOperation.DeliveryConstraints);

            return channel.PublishMessage(transportOperation, properties);
        }
    }
}
