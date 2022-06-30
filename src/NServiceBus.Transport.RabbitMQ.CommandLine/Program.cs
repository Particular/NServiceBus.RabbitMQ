using System.CommandLine;
using NServiceBus.Transport.RabbitMQ.CommandLine;

var rootCommand = new RootCommand("A tool to manage the RabbitMQ transport for NServiceBus endpoints");

CreateDelaysCommand(rootCommand);
CreateEndpointCommand(rootCommand);
CreateQueueCommand(rootCommand);

return await rootCommand.InvokeAsync(args).ConfigureAwait(false);

void CreateDelaysCommand(Command rootCommand)
{
    var delaysSubCommand = new Command("delays", "Commands to create and manage the queues and exchanges for the NServiceBus delay infrastructure");

    delaysSubCommand.AddCommand(DelaysCreateCommand.CreateCommand());
    delaysSubCommand.AddCommand(DelaysMigrateCommand.CreateCommand());
    delaysSubCommand.AddCommand(DelaysVerifyCommand.CreateCommand());

    rootCommand.AddCommand(delaysSubCommand);
}

void CreateEndpointCommand(Command rootCommand)
{
    var endpointSubCommand = new Command("endpoint", "Commands to manage the infrastructure for an NServiceBus endpoint");

    endpointSubCommand.AddCommand(EndpointCreateCommand.CreateCommand());

    rootCommand.AddCommand(endpointSubCommand);
}

void CreateQueueCommand(Command rootCommand)
{
    var queueSubCommand = new Command("queue", "Commands to manage individual queues");

    queueSubCommand.AddCommand(QueueMigrateCommand.CreateCommand());

    rootCommand.AddCommand(queueSubCommand);
}
