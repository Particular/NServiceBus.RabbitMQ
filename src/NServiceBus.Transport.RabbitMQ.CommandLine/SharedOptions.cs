namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System.CommandLine;

    static class SharedOptions
    {
        const string ConnectionStringEnvironmentVariable = "RabbitMQTransport_ConnectionString";

        public static Option<string> CreateConnectionStringOption()
        {
            var connectionStringOption = new Option<string>(
                name: "--connectionString",
                description: $"Overrides environment variable '{ConnectionStringEnvironmentVariable}'",
                getDefaultValue: () => Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable) ?? string.Empty);

            connectionStringOption.AddAlias("-c");

            return connectionStringOption;
        }

        public static Option<Topology> CreateRoutingTopologyOption()
        {
            var topologyOption = new Option<Topology>(
                name: "--topology",
                description: $"Defines which routing toplogy is being used, defaults to Conventional",
                getDefaultValue: () => Topology.Conventional);

            topologyOption.AddAlias("-t");

            return topologyOption;
        }

        public static Option<bool> CreateUseDurableEntitiesOption()
        {
            var useDurableEntities = new Option<bool>(
                name: "--useDurableEntities",
                description: $"Defines if entities should be created as durable, defaults to true",
                getDefaultValue: () => true);

            useDurableEntities.AddAlias("-d");

            return useDurableEntities;
        }

        public static Option<bool> CreateDisableCertValidationOption()
        {
            var disableCertValidationOption = new Option<bool>(
                name: "--disableCertValidation",
                description: $"Disable remote certificate validation when connecting to the broker",
                getDefaultValue: () => false);

            return disableCertValidationOption;
        }

        public static Option<bool> CreateUseExternalAuthOption()
        {
            var useExternalAuthOption = new Option<bool>(
                name: "--useExternalAuth",
                description: $"Use the external authorization option when connecting to the broker",
                getDefaultValue: () => false);

            return useExternalAuthOption;
        }

        public static Option<string> CreateCertPathOption()
        {
            var certPathOption = new Option<string>(
            name: "--certPath",
            description: $"Set the path to the client certificate file for connecting to the broker");

            return certPathOption;
        }

        public static Option<string> CreateCertPassphraseOption()
        {
            var certPassphraseOption = new Option<string>(
            name: "--certPassphrase",
            description: $"The passphrase for client certificate file for when using a client certificate");

            return certPassphraseOption;
        }

        public static ConnectionFactoryBinder CreateConnectionFactoryBinderWithOptions(Command command)
        {
            var connectionStringOption = CreateConnectionStringOption();
            var certPathOption = CreateCertPathOption();
            var certPassphraseOption = CreateCertPassphraseOption();
            var disableCertVaidationOption = CreateDisableCertValidationOption();
            var useExternalAuthOption = CreateUseExternalAuthOption();

            command.AddOption(connectionStringOption);
            command.AddOption(certPathOption);
            command.AddOption(certPassphraseOption);
            command.AddOption(disableCertVaidationOption);
            command.AddOption(useExternalAuthOption);

            return new ConnectionFactoryBinder(connectionStringOption, certPathOption, certPassphraseOption, disableCertVaidationOption, useExternalAuthOption);
        }
    }
}
