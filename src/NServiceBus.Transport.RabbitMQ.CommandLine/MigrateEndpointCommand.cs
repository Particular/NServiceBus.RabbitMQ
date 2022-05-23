namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System;
    using System.CommandLine;

    class MigrateEndpointCommand
    {
        public static Command CreateCommand()
        {
            var quietModeOption = new Option<bool>(
             name: "--Quiet",
             description: $"Disable console output while running");

            var endpointOption = SharedOptions.CreateConnectionStringOption();


            quietModeOption.AddAlias("-q");

            var migrateCommand = new Command("migrate-to-quorum", "Migrate and existing endpoint to use quorum queues.");

            var endpointArgument = new Argument<string>();

            var connectionStringOption = SharedOptions.CreateConnectionStringOption();
            var topologyOption = SharedOptions.CreateRoutingTopologyOption();
            var useDurableEntitiesOption = SharedOptions.CreateUseDurableEntities();

            migrateCommand.AddArgument(endpointArgument);

            migrateCommand.AddOption(connectionStringOption);
            migrateCommand.AddOption(topologyOption);
            migrateCommand.AddOption(useDurableEntitiesOption);
            migrateCommand.AddOption(quietModeOption);

            migrateCommand.SetHandler(async (string endpoint, string connectionString, Topology routingTopology, bool useDurableEntities, bool quietMode, CancellationToken cancellationToken) =>
            {
                var migrationProcess = new MigrateEndpointCommand();
                await migrationProcess.Run(endpoint, connectionString, routingTopology, useDurableEntities, quietMode, cancellationToken).ConfigureAwait(false);
            }, endpointArgument, connectionStringOption, topologyOption, useDurableEntitiesOption, quietModeOption);

            return migrateCommand;
        }

        public Task Run(string endpoint, string connectionString, Topology routingTopology, bool useDurableEntities, bool quietMode, CancellationToken cancellationToken = default)
        {
            if (!quietMode)
            {
                Console.WriteLine($"Starting migration of {endpoint}");
            }

            if (routingTopology != Topology.Conventional)
            {
                throw new NotSupportedException("Quorum queue migration is only supported for the ConventionalRoutingTopology for the moment.");
            }

            CommandRunner.Run(connectionString, channel =>
            {
                var topology = new ConventionalRoutingTopology(useDurableEntities);

                //TBD
            });

            return Task.CompletedTask;
        }
    }
}