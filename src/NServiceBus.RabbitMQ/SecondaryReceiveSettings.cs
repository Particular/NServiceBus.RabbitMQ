namespace NServiceBus.Transports.RabbitMQ
{
    using System;

    class SecondaryReceiveSettings
    {
        public SecondaryReceiveSettings()
        {
            Enabled = false;
        }

        public SecondaryReceiveSettings(string secondaryReceiveQueue, int maximumConcurrencyLevel)
        {
            if (maximumConcurrencyLevel < 0)
            {
                throw new Exception("Concurrency level must be a positive value");
            }

            Enabled = true;

            SecondaryReceiveQueue = secondaryReceiveQueue;

            MaximumConcurrencyLevel = maximumConcurrencyLevel;
        }

        public bool Enabled { get; private set; }

        public int MaximumConcurrencyLevel { get; private set; }
        public string SecondaryReceiveQueue { get; private set; }
    }
}