namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System.CommandLine;
    using System.Security.Cryptography.X509Certificates;

    class DelaysCreateCommand
    {
        public static Command CreateCommand()
        {
            var command = new Command("create", "Create v2 delay infrastructure queues and exchanges");

            var connectionStringOption = SharedOptions.CreateConnectionStringOption();
            var certPathOption = SharedOptions.CreateCertPathOption();
            var certPassphraseOption = SharedOptions.CreateCertPassphraseOption();

            command.AddOption(connectionStringOption);
            command.AddOption(certPathOption);
            command.AddOption(certPassphraseOption);

            command.SetHandler(async (string connectionString, string certPath, string certPassphrase, CancellationToken cancellationToken) =>
            {
                X509Certificate2? certificate = null;

                if (!string.IsNullOrEmpty(certPath) && !string.IsNullOrWhiteSpace(certPassphrase))
                {
                    certificate = new X509Certificate2(certPath, certPassphrase);
                }

                var createProcess = new DelaysCreateCommand(connectionString, certificate);

                await createProcess.Run(cancellationToken).ConfigureAwait(false);

            }, connectionStringOption, certPathOption, certPassphraseOption);

            return command;
        }

        public DelaysCreateCommand(string connectionString, X509Certificate2? certificate)
        {
            this.connectionString = connectionString;
            this.certificate = certificate;
        }

        public Task Run(CancellationToken cancellationToken = default)
        {
            try
            {
                CommandRunner.Run(connectionString, certificate, channel => DelayInfrastructure.Build(channel));

                Console.WriteLine("Delay infrastructure v2 created successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fail: {ex.Message}");
            }

            return Task.CompletedTask;
        }

        readonly string connectionString;
        readonly X509Certificate2? certificate;
    }
}
