using System;
using System.IO;
using static Bullseye.Targets;

internal static class Program
{
    private static readonly string testPackageBaseOutput = Path.GetDirectoryName(Uri.UnescapeDataString(new UriBuilder(typeof(Program).Assembly.CodeBase).Path));

    public static void Main(string[] args)
    {
        Target("delete-virtual-host", () => Broker.DeleteVirtualHost());

        Target("create-virtual-host", () => Broker.CreateVirtualHost());

        Target("add-user-to-virtual-host", () => Broker.AddUserToVirtualHost());

        Target("default", DependsOn("delete-virtual-host", "create-virtual-host", "add-user-to-virtual-host"));

        RunTargetsAndExit(args);
    }
}
