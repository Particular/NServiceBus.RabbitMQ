using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ApprovalTests;
using NUnit.Framework;
using PublicApiGenerator;

[TestFixture]
public class APIApprovals
{
    [Test]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Approve()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "NServiceBus.Transports.RabbitMQ.dll");
        var assembly = Assembly.LoadFile(path);
        var publicApi = Filter(ApiGenerator.GeneratePublicApi(assembly));
        Approvals.Verify(publicApi);
    }

    string Filter(string text) => string.Join(
        Environment.NewLine,
        text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !line.StartsWith("[assembly: ReleaseDateAttribute("))
            .Where(line => !string.IsNullOrWhiteSpace(line)));
}
