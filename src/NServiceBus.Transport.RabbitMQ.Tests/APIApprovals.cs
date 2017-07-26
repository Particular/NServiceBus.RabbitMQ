#if NET452
using System.Runtime.CompilerServices;
using ApprovalTests;
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
        var publicApi = ApiGenerator.GeneratePublicApi(typeof(RabbitMQTransport).Assembly);
        Approvals.Verify(publicApi);
    }
}
#endif