﻿namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System.CommandLine;
    using System.CommandLine.Binding;
    using System.Security.Cryptography.X509Certificates;
    using NServiceBus.Transport.RabbitMQ.ManagementApi;

    class BrokerConnectionBinder(Option<string> connectionStringOption, Option<string> connectionStringEnvOption, Option<string> managementApiUrlOption, Option<string> managementApiUserNameOption, Option<string> managementApiPasswordOption, Option<string> certPathOption,
        Option<string> certPassphraseOption, Option<bool> disableCertificateValidationOption, Option<bool> useExternalAuthOption) : BinderBase<BrokerConnection>
    {
        protected override BrokerConnection GetBoundValue(BindingContext bindingContext)
        {
            var connectionStringOptionValue = bindingContext.ParseResult.GetValueForOption(connectionStringOption);
            var connectionStringEnvOptionValue = bindingContext.ParseResult.GetValueForOption(connectionStringEnvOption);
            var managementApiUrl = bindingContext.ParseResult.GetValueForOption(managementApiUrlOption);
            var managementApiUserName = bindingContext.ParseResult.GetValueForOption(managementApiUserNameOption);
            var managementApiPassword = bindingContext.ParseResult.GetValueForOption(managementApiPasswordOption);
            var certPath = bindingContext.ParseResult.GetValueForOption(certPathOption);
            var certPassphrase = bindingContext.ParseResult.GetValueForOption(certPassphraseOption);
            var disableCertificateValidation = bindingContext.ParseResult.GetValueForOption(disableCertificateValidationOption);
            var useExternalAuth = bindingContext.ParseResult.GetValueForOption(useExternalAuthOption);

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

            var connectionFactory = new ConnectionFactory("rabbitmq-transport", connectionConfiguration, certificateCollection, disableCertificateValidation, useExternalAuth, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(10), []);
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
