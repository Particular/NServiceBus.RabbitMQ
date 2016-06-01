namespace NServiceBus.Transport.RabbitMQ
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Extensibility;
    using Transports;

    class MessageDispatcher : IDispatchMessages
    {
        readonly IChannelProvider channelProvider;

        public MessageDispatcher(IChannelProvider channelProvider)
        {
            this.channelProvider = channelProvider;
        }

        public Task Dispatch(TransportOperations operations, ContextBag context)
        {
            var channel = channelProvider.GetPublishChannel();

            try
            {
                var tasks = new List<Task>(operations.UnicastTransportOperations.Count() + operations.MulticastTransportOperations.Count());

                foreach (var operation in operations.UnicastTransportOperations)
                {
                    tasks.Add(SendMessage(operation, channel));
                }

                foreach (var operation in operations.MulticastTransportOperations)
                {
                    tasks.Add(PublishMessage(operation, channel));
                }

                return tasks.Count == 1 ? tasks[0] : Task.WhenAll(tasks);
            }
            finally
            {
                channelProvider.ReturnPublishChannel(channel);
            }
        }

        Task SendMessage(UnicastTransportOperation transportOperation, ConfirmsAwareChannel channel)
        {
            var message = transportOperation.Message;

            var properties = channel.CreateBasicProperties();
            properties.Fill(message, transportOperation.DeliveryConstraints);

            return channel.SendMessage(transportOperation.Destination, message, properties);
        }

        Task PublishMessage(MulticastTransportOperation transportOperation, ConfirmsAwareChannel channel)
        {
            var message = transportOperation.Message;

            var properties = channel.CreateBasicProperties();
            properties.Fill(message, transportOperation.DeliveryConstraints);

            return channel.PublishMessage(transportOperation.MessageType, message, properties);
        }
    }
}
