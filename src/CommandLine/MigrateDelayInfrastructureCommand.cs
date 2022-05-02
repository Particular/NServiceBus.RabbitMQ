namespace NServiceBus.RabbitMQ.CommandLine
{
    using System;
    using System.CommandLine;
    using System.Threading.Tasks;

    public class MigrateDelayInfrastructureCommand
    {
        const string ConnectionStringEnvironmentVariable = "RabbitMQTransport_ConnectionString";

        public static Command CreateCommand()
        {
            var connectionStringOption = new Option<string>(
                    name: "--connectionString",
                    description: $"Overrides environment variable '{ConnectionStringEnvironmentVariable}'",
                    getDefaultValue: () => Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable) ?? string.Empty);

            connectionStringOption.AddAlias("-c");

            var migrateCommand = new Command("migrate-delay-infrastructure", "Migrate existing delay queues and in-flight delayed messages to the latest infrustructure.");
            migrateCommand.AddOption(connectionStringOption);

            migrateCommand.SetHandler(async (string connectionString, CancellationToken cancellationToken) =>
            {
                var migrationProcess = new MigrateDelayInfrastructureCommand();
                await migrationProcess.Execute(connectionString, cancellationToken).ConfigureAwait(false);

            }, connectionStringOption);

            return migrateCommand;
        }

        public Task Execute(string connectionString, CancellationToken cancellationToken = default)
        {
            Console.WriteLine("Migrating delay infrustructure...");
            return Task.CompletedTask;
        }
    }
}
