using System.CommandLine;
using NServiceBus.Transport.RabbitMQ.CommandLine;
using NServiceBus.Transport.RabbitMQ.CommandLine.Commands.Endpoint;

var rootCommand = new RootCommand("A .NET global tool to manage the RabbitMQ transport for NServiceBus endpoints");
var delaysSubCommand = new Command("delays", "A set of commands that provide functionality related to the delay infrastructure");
var queueSubCommand = new Command("queue", "A set of commands that provide functionality related to endpoints");

delaysSubCommand.AddCommand(DelaysCreateCommand.CreateCommand());
delaysSubCommand.AddCommand(DelaysMigrateCommand.CreateCommand());
delaysSubCommand.AddCommand(DelaysVerifyCommand.CreateCommand());

queueSubCommand.AddCommand(QueueCreateCommand.CreateCommand());

rootCommand.AddCommand(delaysSubCommand);
rootCommand.AddCommand(queueSubCommand);

return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
