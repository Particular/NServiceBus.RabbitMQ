namespace NServiceBus.Transport.RabbitMQ.Tests
{
    using System;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using NServiceBus;
    using NUnit.Framework;
    using PublicApiGenerator;

    [TestFixture]
    public class APIApprovals
    {
        [Test]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Approve()
        {
            var publicApi = Filter(ApiGenerator.GeneratePublicApi(typeof(RabbitMQTransport).Assembly));
            TestApprover.Verify(publicApi);
        }

        string Filter(string api)
        {
            var lines = api.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => !line.StartsWith("[assembly: System.Runtime.Versioning.TargetFrameworkAttribute"))
                .Where(line => !string.IsNullOrWhiteSpace(line));
            
            return string.Join(Environment.NewLine, lines);
        }
    }
}
