namespace NServiceBus.Transports.RabbitMQ
{
    using System;

    class ReceiveOptions
    {
        public MessageConverter Converter { get; private set; }
        public ushort PrefetchCount { get; private set; }
        public int DequeueTimeout { get; private set; }
        public bool PurgeOnStartup { get; private set; }

        public ReceiveOptions(Func<string, SecondaryReceiveSettings> getSecondaryReceiveSettings, MessageConverter converter, ushort prefetchCount, int dequeueTimeout,bool purgeOnStartup)
        {
            Converter = converter;
            PrefetchCount = prefetchCount;
            DequeueTimeout = dequeueTimeout;
            PurgeOnStartup = purgeOnStartup;
            secondaryReceiveSettings = getSecondaryReceiveSettings;
        }

        public SecondaryReceiveSettings GetSettings(string queue)
        {
            return secondaryReceiveSettings(queue);
        }

        Func<string, SecondaryReceiveSettings> secondaryReceiveSettings;

    }
}