namespace NServiceBus.Transports.RabbitMQ
{
    using System;

    class SecondaryReceiveConfiguration
    {
        public SecondaryReceiveConfiguration(Func<string, SecondaryReceiveSettings> getSecondaryReceiveSettings)
        {
            secondaryReceiveSettings = getSecondaryReceiveSettings;
        }

        public SecondaryReceiveSettings GetSettings(string queue)
        {
            return secondaryReceiveSettings(queue);
        }

        Func<string, SecondaryReceiveSettings> secondaryReceiveSettings;

    }
}