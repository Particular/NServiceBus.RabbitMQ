namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System.CommandLine;

    class InstallDelayInfrastructureCommand
    {
        public static Command CreateCommand(Option<string> connectionStringOption)
        {
            var delayInstallerCommand = new Command("install-delay-infrastructure", "Create delay infrastructure queues and exchanges");
            delayInstallerCommand.AddOption(connectionStringOption);

            delayInstallerCommand.SetHandler((string connectionString) =>
            {
                CommandRunner.Run(connectionString, channel => DelayInfrastructure.Build(channel));
            }, connectionStringOption);

            return delayInstallerCommand;
        }
    }
}
