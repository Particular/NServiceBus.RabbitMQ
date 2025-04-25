namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System.CommandLine;
    using System.CommandLine.Binding;
    using NServiceBus.Transport.RabbitMQ.ManagementApi;

    class BrokerVerifierBinder(Option<string> connectionStringOption, Option<string> connectionStringEnvOption, Option<string> managementApiUrlOption, Option<string> managementApiUserNameOption, Option<string> managementApiPasswordOption, Option<bool> disableCertificateValidationOption) : BinderBase<BrokerVerifier>
    {
        protected override BrokerVerifier GetBoundValue(BindingContext bindingContext)
        {
            var connectionStringOptionValue = bindingContext.ParseResult.GetValueForOption(connectionStringOption);
            var connectionStringEnvOptionValue = bindingContext.ParseResult.GetValueForOption(connectionStringEnvOption);
            var managementApiUrl = bindingContext.ParseResult.GetValueForOption(managementApiUrlOption);
            var managementApiUserName = bindingContext.ParseResult.GetValueForOption(managementApiUserNameOption);
            var managementApiPassword = bindingContext.ParseResult.GetValueForOption(managementApiPasswordOption);
            var disableCertificateValidation = bindingContext.ParseResult.GetValueForOption(disableCertificateValidationOption);

            var connectionString = GetConnectionString(connectionStringOptionValue, connectionStringEnvOptionValue);

            var connectionConfiguration = ConnectionConfiguration.Create(connectionString);
            var managementApiConfiguration = ManagementApiConfiguration.Create(managementApiUrl, managementApiUserName, managementApiPassword);

            var managementClient = new ManagementClient(connectionConfiguration, managementApiConfiguration, disableCertificateValidation);
            var brokerVerifier = new BrokerVerifier(managementClient, true);

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
