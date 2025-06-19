using System.CommandLine;
using NServiceBus.Transport.RabbitMQ.CommandLine;

var rootCommand = new RootCommand("A tool to manage the RabbitMQ transport for NServiceBus endpoints");

CreateDelaysCommand(rootCommand);
CreateEndpointCommand(rootCommand);
CreateQueueCommand(rootCommand);

var parseResult = rootCommand.Parse(args);

return await parseResult.InvokeAsync();

void CreateDelaysCommand(Command rootCommand)
{
    var delaysSubCommand = new Command("delays", "Commands to manage the queues and exchanges for the NServiceBus delay infrastructure");

    delaysSubCommand.Subcommands.Add(DelaysCreateCommand.CreateCommand());
    delaysSubCommand.Subcommands.Add(DelaysMigrateCommand.CreateCommand());
    delaysSubCommand.Subcommands.Add(DelaysVerifyCommand.CreateCommand());

    rootCommand.Subcommands.Add(delaysSubCommand);
}

void CreateEndpointCommand(Command rootCommand)
{
    var endpointSubCommand = new Command("endpoint", "Commands to manage the infrastructure for an NServiceBus endpoint");

    endpointSubCommand.Subcommands.Add(EndpointCreateCommand.CreateCommand());

    rootCommand.Subcommands.Add(endpointSubCommand);
}

void CreateQueueCommand(Command rootCommand)
{
    var queueSubCommand = new Command("queue", "Commands to manage individual queues");

    queueSubCommand.Subcommands.Add(QueueMigrateCommand.CreateCommand());
    queueSubCommand.Subcommands.Add(QueueValidateDeliveryLimitCommand.CreateCommand());

    rootCommand.Subcommands.Add(queueSubCommand);
}
