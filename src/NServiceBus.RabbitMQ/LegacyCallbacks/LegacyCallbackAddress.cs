namespace NServiceBus.Transports.RabbitMQ
{
    class LegacyCallbackAddress
    {
        public string Address { get; }

        public LegacyCallbackAddress(string address)
        {
            Address = address;
        }
    }
}