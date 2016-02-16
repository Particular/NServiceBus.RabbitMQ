namespace NServiceBus.Transports.RabbitMQ
{
    class ReceiveOptions
    {
        public MessageConverter Converter { get; }
        public bool PurgeOnStartup { get; }
        public string ConsumerTag { get; }

        public ReceiveOptions(MessageConverter converter, bool purgeOnStartup, string consumerTag)
        {
            Converter = converter;
            PurgeOnStartup = purgeOnStartup;
            ConsumerTag = consumerTag;
        }
    }
}