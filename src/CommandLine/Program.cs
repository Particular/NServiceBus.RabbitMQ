using System.CommandLine;
using NServiceBus.RabbitMQ.CommandLine;

var rootCommand = new RootCommand("A .NET global tool to manage the RabbitMQ transport for NServiceBus endpoints");

rootCommand.AddCommand(MigrateDelayInfrastructureCommand.CreateCommand());

return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
