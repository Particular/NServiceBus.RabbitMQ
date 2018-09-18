#r "script-packages/Bullseye.1.0.0-rc.4/lib/netstandard2.0/Bullseye.dll"
#load "scripts/broker.csx"

using static Bullseye.Targets;

Add("delete-virtual-host", () => DeleteVirtualHost());

Add("create-virtual-host", () => CreateVirtualHost());

Add("add-user-to-virtual-host", () => AddUserToVirtualHost());

Add("default", DependsOn("delete-virtual-host", "create-virtual-host", "add-user-to-virtual-host"));

Run(Args);
