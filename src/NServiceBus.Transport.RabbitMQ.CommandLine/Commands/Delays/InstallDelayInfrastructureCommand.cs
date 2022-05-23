namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System.CommandLine;
    using System.Security.Cryptography.X509Certificates;

    class InstallDelayInfrastructureCommand
    {
        public static Command CreateCommand()
        {
            var delayInstallerCommand = new Command("create", "Create delay infrastructure queues and exchanges");
            var connectionStringOption = SharedOptions.CreateConnectionStringOption();
            var certPathOption = SharedOptions.CreateCertPathOption();
            var certPassphraseOption = SharedOptions.CreateCertPassphraseOption();

            delayInstallerCommand.AddOption(connectionStringOption);
            delayInstallerCommand.AddOption(certPathOption);
            delayInstallerCommand.AddOption(certPassphraseOption);

            delayInstallerCommand.SetHandler((string connectionString, string certPath, string certPassphrase) =>
            {
                X509Certificate2? certificate = null;

                if (!string.IsNullOrEmpty(certPath) && !string.IsNullOrWhiteSpace(certPassphrase))
                {
                    certificate = new X509Certificate2(certPath, certPassphrase);
                }

                CommandRunner.Run(connectionString, certificate, channel => DelayInfrastructure.Build(channel));
            }, connectionStringOption, certPathOption, certPassphraseOption);

            return delayInstallerCommand;
        }
    }
}
