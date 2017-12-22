namespace NServiceBus.Transport.RabbitMQ
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Extensibility;

    class MessageDispatcher : IDispatchMessages
    {
        readonly ChannelPool channelPool;

        public MessageDispatcher(ChannelPool channelPool)
        {
            this.channelPool = channelPool;
        }

        public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, ContextBag context)
        {
            var channel = channelPool.GetChannel();

            try
            {
                var unicastTransportOperations = outgoingMessages.UnicastTransportOperations;
                var multicastTransportOperations = outgoingMessages.MulticastTransportOperations;

                var tasks = new List<Task>(unicastTransportOperations.Count + multicastTransportOperations.Count);

                foreach (var operation in unicastTransportOperations)
                {
                    tasks.Add(SendMessage(operation, channel));
                }

                foreach (var operation in multicastTransportOperations)
                {
                    tasks.Add(PublishMessage(operation, channel));
                }

                return tasks.Count == 1 ? tasks[0] : Task.WhenAll(tasks);
            }
            finally
            {
                channelPool.ReturnChannel(channel);
            }
        }

        Task SendMessage(UnicastTransportOperation transportOperation, ConfirmsAwareChannel channel)
        {
            var message = transportOperation.Message;

            var properties = channel.CreateBasicProperties();
            properties.Fill(message, transportOperation.DeliveryConstraints, out var destination);

            return channel.SendMessage(destination ?? transportOperation.Destination, message, properties);
        }

        Task PublishMessage(MulticastTransportOperation transportOperation, ConfirmsAwareChannel channel)
        {
            var message = transportOperation.Message;

            var properties = channel.CreateBasicProperties();
            properties.Fill(message, transportOperation.DeliveryConstraints, out _);

            return channel.PublishMessage(transportOperation.MessageType, message, properties);
        }
    }
}
