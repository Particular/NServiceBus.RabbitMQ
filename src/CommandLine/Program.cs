using System.CommandLine;
using NServiceBus.Transport.RabbitMQ.CommandLine;

var rootCommand = new RootCommand("A .NET global tool to manage the RabbitMQ transport for NServiceBus endpoints");

rootCommand.AddCommand(MigrateDelayInfrastructureCommand.CreateCommand());
rootCommand.AddCommand(InstallDelayInfrastructureCommand.CreateCommand());

return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
