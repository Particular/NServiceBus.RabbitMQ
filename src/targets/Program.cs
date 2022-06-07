using System.Threading.Tasks;
using static Bullseye.Targets;

static class Program
{
    public static async Task Main(string[] args)
    {
        Target("delete-virtual-host", () => Broker.DeleteVirtualHost());

        Target("create-virtual-host", () => Broker.CreateVirtualHost());

        Target("add-user-to-virtual-host", () => Broker.AddUserToVirtualHost());

        Target("default", DependsOn("delete-virtual-host", "create-virtual-host", "add-user-to-virtual-host"));

        await RunTargetsAndExitAsync(args).ConfigureAwait(false);
    }
}
