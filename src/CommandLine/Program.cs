using System.CommandLine;
using NServiceBus.Transport.RabbitMQ.CommandLine;

const string ConnectionStringEnvironmentVariable = "RabbitMQTransport_ConnectionString";

var connectionStringOption = new Option<string>(
        name: "--connectionString",
        description: $"Overrides environment variable '{ConnectionStringEnvironmentVariable}'",
        getDefaultValue: () => Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable) ?? string.Empty);

connectionStringOption.AddAlias("-c");

var rootCommand = new RootCommand("A .NET global tool to manage the RabbitMQ transport for NServiceBus endpoints");

rootCommand.AddCommand(MigrateDelayInfrastructureCommand.CreateCommand(connectionStringOption));
rootCommand.AddCommand(InstallDelayInfrastructureCommand.CreateCommand(connectionStringOption));

return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
