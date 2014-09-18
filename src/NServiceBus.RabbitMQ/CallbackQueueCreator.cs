namespace NServiceBus.Transports.RabbitMQ
{
    using NServiceBus.Unicast.Queuing;

    class CallbackQueueCreator : IWantQueueCreated
    {
        public Address CallbackQueueAddress { get; set; }

        public Address Address
        {
            get { return CallbackQueueAddress; }
        }

        public bool Enabled { get; set; }

        public bool ShouldCreateQueue()
        {
            return Enabled;
        }
    }
}