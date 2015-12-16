namespace NServiceBus.Transports.RabbitMQ
{
    class CallbackAddress
    {
        public const string HeaderKey = "NServiceBus.RabbitMQ.CallbackQueue";

        public string Value { get; }
        public bool HasValue { get; }

        public CallbackAddress(string value)
        {
            HasValue = true;
            Value = value;
        }

        private CallbackAddress()
        {
            HasValue = false;
        }

        public static CallbackAddress None()
        {
            return new CallbackAddress();
        }
    }
}