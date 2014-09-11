namespace NServiceBus.Transports.RabbitMQ
{
    using System.Collections.Generic;

    class SecondaryReceiveSettings
    {
        public int MaximumConcurrencyLevel;
        public List<string> SecondaryQueues = new List<string>();
    }
}