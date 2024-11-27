namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System.CommandLine;
    using System.CommandLine.Binding;
    using System.Security.Cryptography.X509Certificates;

    class BrokerConnectionBinder : BinderBase<BrokerConnection>
    {
        public BrokerConnectionBinder(Option<string> connectionStringOption, Option<string> connectionStringEnvOption, Option<string> certPathOption, Option<string> certPassphraseOption, Option<bool> disableCertificateValidationOption, Option<bool> useExternalAuthOption)
        {
            this.connectionStringOption = connectionStringOption;
            this.connectionStringEnvOption = connectionStringEnvOption;
            this.certPathOption = certPathOption;
            this.certPassphraseOption = certPassphraseOption;
            this.disableCertificateValidationOption = disableCertificateValidationOption;
            this.useExternalAuthOption = useExternalAuthOption;
        }

        protected override BrokerConnection GetBoundValue(BindingContext bindingContext)
        {
            var connectionStringOptionValue = bindingContext.ParseResult.GetValueForOption(connectionStringOption);
            var connectionStringEnvOptionValue = bindingContext.ParseResult.GetValueForOption(connectionStringEnvOption);
            var certPath = bindingContext.ParseResult.GetValueForOption(certPathOption);
            var certPassphrase = bindingContext.ParseResult.GetValueForOption(certPassphraseOption);
            var disableCertificateValidation = bindingContext.ParseResult.GetValueForOption(disableCertificateValidationOption);
            var useExternalAuth = bindingContext.ParseResult.GetValueForOption(useExternalAuthOption);

            var connectionString = GetConnectionString(connectionStringOptionValue, connectionStringEnvOptionValue);

            var connectionConfiguration = ConnectionConfiguration.Create(connectionString);
            var certificateCollection = new X509Certificate2Collection();

            if (certPath != null)
            {
                var certificate = new X509Certificate2(certPath, certPassphrase);
                certificateCollection.Add(certificate);
            }

            var connectionFactory = new ConnectionFactory("rabbitmq-transport", connectionConfiguration, certificateCollection, disableCertificateValidation, useExternalAuth, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(10), []);
            var brokerConnection = new BrokerConnection(connectionFactory);

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

        readonly Option<string> connectionStringOption;
        readonly Option<string> connectionStringEnvOption;
        readonly Option<string> certPathOption;
        readonly Option<string> certPassphraseOption;
        readonly Option<bool> disableCertificateValidationOption;
        readonly Option<bool> useExternalAuthOption;
    }
}
