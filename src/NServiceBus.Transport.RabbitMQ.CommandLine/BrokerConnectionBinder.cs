namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System.CommandLine;
    using System.Security.Cryptography.X509Certificates;
    using NServiceBus.Transport.RabbitMQ.ManagementApi;

    class BrokerConnectionBinder(Option<string> connectionStringOption, Option<string> connectionStringEnvOption, Option<string> managementApiUrlOption, Option<string> managementApiUserNameOption, Option<string> managementApiPasswordOption, Option<string> certPathOption,
        Option<string> certPassphraseOption, Option<bool> disableCertificateValidationOption, Option<bool> useExternalAuthOption)
    {
        public BrokerConnection CreateBrokerConnection(ParseResult parseResult)
        {
            var connectionStringOptionValue = parseResult.GetValue(connectionStringOption);
            var connectionStringEnvOptionValue = parseResult.GetValue(connectionStringEnvOption);
            var managementApiUrl = parseResult.GetValue(managementApiUrlOption);
            var managementApiUserName = parseResult.GetValue(managementApiUserNameOption);
            var managementApiPassword = parseResult.GetValue(managementApiPasswordOption);
            var certPath = parseResult.GetValue(certPathOption);
            var certPassphrase = parseResult.GetValue(certPassphraseOption);
            var disableCertificateValidation = parseResult.GetValue(disableCertificateValidationOption);
            var useExternalAuth = parseResult.GetValue(useExternalAuthOption);

            var connectionString = GetConnectionString(connectionStringOptionValue, connectionStringEnvOptionValue);

            var connectionConfiguration = ConnectionConfiguration.Create(connectionString);
            var managementApiConfiguration = ManagementApiConfiguration.Create(managementApiUrl, managementApiUserName, managementApiPassword);

            var managementClient = new ManagementClient(connectionConfiguration, managementApiConfiguration, disableCertificateValidation);
            var brokerVerifier = new BrokerVerifier(managementClient, BrokerRequirementChecks.None, true);

            X509Certificate2Collection? certificateCollection = null;

            if (certPath is not null)
            {
                certificateCollection = [CertificateLoader.LoadCertificateFromFile(certPath, certPassphrase)];
            }

            var connectionFactory = new ConnectionFactory("rabbitmq-transport", connectionConfiguration, certificateCollection, disableCertificateValidation, useExternalAuth, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(10), [], []);
            var brokerConnection = new BrokerConnection(brokerVerifier, connectionFactory);

            return brokerConnection;
        }

        static string GetConnectionString(string? connectionString, string? connectionStringEnv)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                var environment = Environment.GetEnvironmentVariable(connectionStringEnv ?? string.Empty);

                if (environment != null)
                {
                    return environment;
                }
            }

            return connectionString ?? string.Empty;
        }
    }
}
