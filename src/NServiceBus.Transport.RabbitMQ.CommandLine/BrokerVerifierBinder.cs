namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System.CommandLine;
    using NServiceBus.Transport.RabbitMQ.ManagementApi;

    class BrokerVerifierBinder(Option<string> connectionStringOption, Option<string> connectionStringEnvOption, Option<string> managementApiUrlOption, Option<string> managementApiUserNameOption, Option<string> managementApiPasswordOption, Option<bool> disableCertificateValidationOption)
    {
        public BrokerVerifier CreateBrokerVerifier(ParseResult parseResult)
        {
            var connectionStringOptionValue = parseResult.GetValue(connectionStringOption);
            var connectionStringEnvOptionValue = parseResult.GetValue(connectionStringEnvOption);
            var managementApiUrl = parseResult.GetValue(managementApiUrlOption);
            var managementApiUserName = parseResult.GetValue(managementApiUserNameOption);
            var managementApiPassword = parseResult.GetValue(managementApiPasswordOption);
            var disableCertificateValidation = parseResult.GetValue(disableCertificateValidationOption);

            var connectionString = GetConnectionString(connectionStringOptionValue, connectionStringEnvOptionValue);

            var connectionConfiguration = ConnectionConfiguration.Create(connectionString);
            var managementApiConfiguration = ManagementApiConfiguration.Create(managementApiUrl, managementApiUserName, managementApiPassword);

            var managementClient = new ManagementClient(connectionConfiguration, managementApiConfiguration, disableCertificateValidation);
            var brokerVerifier = new BrokerVerifier(managementClient, BrokerRequirementChecks.None, true);

            return brokerVerifier;
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
