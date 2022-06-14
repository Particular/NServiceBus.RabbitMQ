using System.CommandLine;
using NServiceBus.Transport.RabbitMQ.CommandLine;

var rootCommand = new RootCommand("A .NET global tool to manage the RabbitMQ transport for NServiceBus endpoints");

CreateDelaysCommand(rootCommand);
CreateEndpointCommand(rootCommand);

return await rootCommand.InvokeAsync(args).ConfigureAwait(false);


void CreateDelaysCommand(Command rootCommand)
{
    var delaysSubCommand = new Command("delays", "A set of commands that provide functionality related to the delay infrastructure");

    delaysSubCommand.AddCommand(DelaysCreateCommand.CreateCommand());
    delaysSubCommand.AddCommand(DelaysMigrateCommand.CreateCommand());
    delaysSubCommand.AddCommand(DelaysVerifyCommand.CreateCommand());

    rootCommand.AddCommand(delaysSubCommand);
}

void CreateEndpointCommand(Command rootCommand)
{
    var endpointSubCommand = new Command("endpoint", "A set of commands that provide functionality related to endpoints");

    endpointSubCommand.AddCommand(EndpointCreateCommand.CreateCommand());

    rootCommand.AddCommand(endpointSubCommand);
}