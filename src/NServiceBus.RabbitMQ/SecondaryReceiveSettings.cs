namespace NServiceBus.Transports.RabbitMQ
{
    using System;

    class SecondaryReceiveSettings
    {
        public static SecondaryReceiveSettings Disabled()
        {
            return new SecondaryReceiveSettings();
        }

        public static SecondaryReceiveSettings Enabled(string secondaryReceiveQueue, int maximumConcurrencyLevel)
        {
            if (maximumConcurrencyLevel <= 0)
            {
                throw new ArgumentException("Concurrency level must be a positive value.", nameof(maximumConcurrencyLevel));
            }
            if (string.IsNullOrEmpty(secondaryReceiveQueue))
            {
                throw new ArgumentException("Receive queue must not be empty.", nameof(secondaryReceiveQueue));
            }
            return new SecondaryReceiveSettings
            {
                ReceiveQueue = secondaryReceiveQueue,
                MaximumConcurrencyLevel = maximumConcurrencyLevel
            };
        }

        public bool IsEnabled => MaximumConcurrencyLevel > 0;
        public int MaximumConcurrencyLevel { get; private set; }
        public string ReceiveQueue { get; private set; }

        private SecondaryReceiveSettings()
        {
        }
    }
}