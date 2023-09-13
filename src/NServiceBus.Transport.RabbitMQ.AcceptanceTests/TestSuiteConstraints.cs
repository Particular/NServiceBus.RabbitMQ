namespace NServiceBus.AcceptanceTests
{
    using System.Runtime.CompilerServices;
    using AcceptanceTesting.Support;

    public partial class TestSuiteConstraints : ITestSuiteConstraints
    {
        public bool SupportsDtc => false;

        public bool SupportsCrossQueueTransactions => false;

        public bool SupportsNativePubSub => true;

        public bool SupportsDelayedDelivery => true;

        public bool SupportsOutbox => true;

        public bool SupportsPurgeOnStartup => true;

        public IConfigureEndpointTestExecution CreateTransportConfiguration() => new ConfigureEndpointRabbitMQTransport();

        public IConfigureEndpointTestExecution CreatePersistenceConfiguration() => new ConfigureEndpointAcceptanceTestingPersistence();

        [ModuleInitializer]
        public static void Initialize() => ITestSuiteConstraints.Current = new TestSuiteConstraints();
    }
}
