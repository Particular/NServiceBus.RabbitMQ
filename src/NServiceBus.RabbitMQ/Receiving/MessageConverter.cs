namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using global::RabbitMQ.Client.Events;

    static class MessageConverter
    {
        public static string DefaultMessageIdStrategy(BasicDeliverEventArgs message)
        {
            var properties = message.BasicProperties;

            if (!properties.IsMessageIdPresent() || string.IsNullOrWhiteSpace(properties.MessageId))
            {
                throw new InvalidOperationException("A non-empty 'message-id' property is required when running NServiceBus on top of RabbitMQ. If this is an interop message, then set the 'message-id' property before publishing the message");
            }

            return properties.MessageId;
        }
    }
}