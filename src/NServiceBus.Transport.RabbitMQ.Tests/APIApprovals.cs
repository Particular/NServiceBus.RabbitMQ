#if NET452
using System;
using System.Linq;
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
        var publicApi = Filter(ApiGenerator.GeneratePublicApi(typeof(RabbitMQTransport).Assembly));
        Approvals.Verify(publicApi);
    }

    string Filter(string text)
    {
        return string.Join(Environment.NewLine, text.Split(new[]
        {
            Environment.NewLine
        }, StringSplitOptions.RemoveEmptyEntries)
            .Where(l => !l.StartsWith("[assembly: ReleaseDateAttribute("))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            );
    }

}
#endif