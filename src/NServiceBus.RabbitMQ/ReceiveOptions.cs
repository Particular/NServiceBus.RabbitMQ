namespace NServiceBus.Transports.RabbitMQ
{
    using System;

    class ReceiveOptions
    {
        public MessageConverter Converter { get; }
        public bool PurgeOnStartup { get; }
        public string ConsumerTag { get; }

        public ReceiveOptions(Func<string, SecondaryReceiveSettings> getSecondaryReceiveSettings,
            MessageConverter converter,
            bool purgeOnStartup,
            string consumerTag)
        {
            Converter = converter;
            PurgeOnStartup = purgeOnStartup;
            ConsumerTag = consumerTag;
            secondaryReceiveSettings = getSecondaryReceiveSettings;
        }

        public SecondaryReceiveSettings GetSettings(string queue)
        {
            return secondaryReceiveSettings(queue);
        }

        Func<string, SecondaryReceiveSettings> secondaryReceiveSettings;

    }
}