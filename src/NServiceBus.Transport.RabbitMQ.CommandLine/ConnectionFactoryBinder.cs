namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System.CommandLine;
    using System.CommandLine.Binding;
    using System.Security.Cryptography.X509Certificates;

    class ConnectionFactoryBinder : BinderBase<ConnectionFactory>
    {
        public ConnectionFactoryBinder(Option<string> connectionStringOption, Option<string> certPathOption, Option<string> certPassphraseOption, Option<bool> disableCertificateValidationOption, Option<bool> useExternalAuthOption)
        {
            this.connectionStringOption = connectionStringOption;
            this.certPathOption = certPathOption;
            this.certPassphraseOption = certPassphraseOption;
            this.disableCertificateValidationOption = disableCertificateValidationOption;
            this.useExternalAuthOption = useExternalAuthOption;
        }

        protected override ConnectionFactory GetBoundValue(BindingContext bindingContext)
        {
            var connectionString = bindingContext.ParseResult.GetValueForOption(connectionStringOption)!;
            var certPath = bindingContext.ParseResult.GetValueForOption(certPathOption);
            var certPassphrase = bindingContext.ParseResult.GetValueForOption(certPassphraseOption);
            var disableCertificateValidation = bindingContext.ParseResult.GetValueForOption(disableCertificateValidationOption);
            var useExternalAuth = bindingContext.ParseResult.GetValueForOption(useExternalAuthOption);

            var connectionConfiguration = ConnectionConfiguration.Create(connectionString);
            var certificateCollection = new X509Certificate2Collection();

            if (certPath != null)
            {
                var certificate = new X509Certificate2(certPath, certPassphrase);
                certificateCollection.Add(certificate);
            }

            var connectionFactory = new ConnectionFactory("rabbitmq-transport", connectionConfiguration, certificateCollection, disableCertificateValidation, useExternalAuth, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(10));

            return connectionFactory;
        }

        readonly Option<string> connectionStringOption;
        readonly Option<string> certPathOption;
        readonly Option<string> certPassphraseOption;
        readonly Option<bool> disableCertificateValidationOption;
        readonly Option<bool> useExternalAuthOption;
    }
}
