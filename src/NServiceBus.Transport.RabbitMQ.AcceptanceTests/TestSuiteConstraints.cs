namespace NServiceBus.AcceptanceTests
{
    using AcceptanceTesting.Support;

    public partial class TestSuiteConstraints
    {
        public bool SupportsDtc => false;

        public bool SupportsCrossQueueTransactions => false;

        public bool SupportsNativePubSub => true;

        public bool SupportsNativeDeferral => true;

        public bool SupportsOutbox => true;

        public IConfigureEndpointTestExecution CreateTransportConfiguration() => new ConfigureEndpointRabbitMQTransport();

        public IConfigureEndpointTestExecution CreatePersistenceConfiguration() => new ConfigureEndpointInMemoryPersistence();
    }
}
