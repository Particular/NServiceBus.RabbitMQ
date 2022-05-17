namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System.CommandLine;

    class InstallDelayInfrastructureCommand
    {
        public static Command CreateCommand()
        {
            var delayInstallerCommand = new Command("create", "Create delay infrastructure queues and exchanges");
            var connectionStringOption = SharedOptions.CreateConnectionStringOption();
            delayInstallerCommand.AddOption(connectionStringOption);

            delayInstallerCommand.SetHandler((string connectionString) =>
            {
                CommandRunner.Run(connectionString, channel => DelayInfrastructure.Build(channel));
            }, connectionStringOption);

            return delayInstallerCommand;
        }
    }
}
