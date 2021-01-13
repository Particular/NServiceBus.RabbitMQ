using static Bullseye.Targets;

static class Program
{
    public static void Main(string[] args)
    {
        Target("delete-virtual-host", () => Broker.DeleteVirtualHost());

        Target("create-virtual-host", () => Broker.CreateVirtualHost());

        Target("add-user-to-virtual-host", () => Broker.AddUserToVirtualHost());

        Target("default", DependsOn("delete-virtual-host", "create-virtual-host", "add-user-to-virtual-host"));

        RunTargetsAndExit(args);
    }
}
