namespace NServiceBus
{
    using Transports;

    /// <summary>
    /// Transport definition for RabbirtMQ
    /// </summary>
    public class RabbitMQ : TransportDefinition
    {
        /// <summary>
        /// Ctor
        /// </summary>
        public RabbitMQ()
        {
            HasNativePubSubSupport = true;
            HasSupportForCentralizedPubSub = true;
            HasSupportForDistributedTransactions = false;
        }
    }
}