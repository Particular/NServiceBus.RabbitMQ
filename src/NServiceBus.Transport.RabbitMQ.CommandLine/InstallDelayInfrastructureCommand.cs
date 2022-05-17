namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System.CommandLine;

    class InstallDelayInfrastructureCommand
    {
        public static Command CreateCommand()
        {
            var delayInstallerCommand = new Command("create", "Create delay infrastructure queues and exchanges");
            delayInstallerCommand.AddOption(SharedOptions.ConnectionString);

            delayInstallerCommand.SetHandler((string connectionString) =>
            {
                CommandRunner.Run(connectionString, channel => DelayInfrastructure.Build(channel));
            }, SharedOptions.ConnectionString);

            return delayInstallerCommand;
        }
    }
}
