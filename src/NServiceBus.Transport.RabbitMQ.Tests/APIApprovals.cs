namespace NServiceBus.Transport.RabbitMQ.Tests
{
    using NServiceBus;
    using NUnit.Framework;
    using Particular.Approvals;
    using PublicApiGenerator;

    [TestFixture]
    public class APIApprovals
    {
        [Test]
        public void Approve()
        {
            var publicApi = ApiGenerator.GeneratePublicApi(typeof(RabbitMQTransport).Assembly, excludeAttributes: new[] { "System.Runtime.Versioning.TargetFrameworkAttribute", "System.Reflection.AssemblyMetadataAttribute" });
            Approver.Verify(publicApi);
        }
    }
}
