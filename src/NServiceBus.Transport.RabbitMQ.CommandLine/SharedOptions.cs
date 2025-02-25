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
                description: $"Force this command to use the specified connection string");

            connectionStringOption.AddAlias("-c");

            return connectionStringOption;
        }

        public static Option<string> CreateConnectionStringEnvOption()
        {
            var connectionStringEnvOption = new Option<string>(
                name: "--connectionStringEnv",
                description: $"Specifies the environment variable where the connection string can be found. --connectionString, if specified, will take precedence over this option.",
                getDefaultValue: () => ConnectionStringEnvironmentVariable);

            return connectionStringEnvOption;
        }

        public static Option<string> CreateManagementApiUrlOption()
        {
            var managementApiUrlOption = new Option<string>(
                name: "--managementApiUrl",
                description: $"Overrides the value inferred from the connection string");

            return managementApiUrlOption;
        }

        public static Option<string> CreateManagementApiUserNameOption()
        {
            var managementApiUserNameOption = new Option<string>(
                name: "--managementApiUserName",
                description: $"Overrides the value inferred from the connection string. If provided, --managementApiUrl and --managementApiPassword must also be provided.");

            return managementApiUserNameOption;
        }

        public static Option<string> CreateManagementApiPasswordOption()
        {
            var managementApiPasswordOption = new Option<string>(
                name: "--managementApiPassword",
                description: $"Overrides the value inferred from the connection string. If provided, --managementApiUrl and --managementApiUserName must also be provided.");

            return managementApiPasswordOption;
        }

        public static Option<RoutingTopologyType> CreateRoutingTopologyTypeOption()
        {
            var routingTopologyTypeOption = new Option<RoutingTopologyType>(
                name: "--routingTopology",
                description: $"Specifies which routing topology to use.",
                getDefaultValue: () => RoutingTopologyType.Conventional);

            routingTopologyTypeOption.AddAlias("-r");

            return routingTopologyTypeOption;
        }

        public static Option<bool> CreateUseDurableEntitiesOption()
        {
            var useDurableEntities = new Option<bool>(
                name: "--useDurableEntities",
                description: $"Specifies if entities should be created as durable",
                getDefaultValue: () => true);

            useDurableEntities.AddAlias("-d");

            return useDurableEntities;
        }

        public static Option<QueueType> CreateQueueTypeOption()
        {
            var queueTypeOption = new Option<QueueType>(
                name: "--queueType",
                description: $"Specifies queue type will be used for queue creation",
                getDefaultValue: () => QueueType.Quorum);

            queueTypeOption.AddAlias("-t");

            return queueTypeOption;
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

        public static BrokerConnectionBinder CreateBrokerConnectionBinderWithOptions(Command command)
        {
            var connectionStringOption = CreateConnectionStringOption();
            var connectionStringEnvOption = CreateConnectionStringEnvOption();
            var managementApiUrlOption = CreateManagementApiUrlOption();
            var managementApiUserNameOption = CreateManagementApiUserNameOption();
            var managementApiPasswordOption = CreateManagementApiPasswordOption();
            var certPathOption = CreateCertPathOption();
            var certPassphraseOption = CreateCertPassphraseOption();
            var disableCertValidationOption = CreateDisableCertValidationOption();
            var useExternalAuthOption = CreateUseExternalAuthOption();

            command.AddOption(connectionStringOption);
            command.AddOption(connectionStringEnvOption);
            command.AddOption(managementApiUrlOption);
            command.AddOption(managementApiUserNameOption);
            command.AddOption(managementApiPasswordOption);
            command.AddOption(certPathOption);
            command.AddOption(certPassphraseOption);
            command.AddOption(disableCertValidationOption);
            command.AddOption(useExternalAuthOption);

            return new BrokerConnectionBinder(connectionStringOption, connectionStringEnvOption, managementApiUrlOption, managementApiUserNameOption, managementApiPasswordOption, certPathOption, certPassphraseOption, disableCertValidationOption, useExternalAuthOption);
        }

        public static BrokerVerifierBinder CreateBrokerVerifierBinderWithOptions(Command command)
        {
            var connectionStringOption = CreateConnectionStringOption();
            var connectionStringEnvOption = CreateConnectionStringEnvOption();
            var managementApiUrlOption = CreateManagementApiUrlOption();
            var managementApiUserNameOption = CreateManagementApiUserNameOption();
            var managementApiPasswordOption = CreateManagementApiPasswordOption();

            command.AddOption(connectionStringOption);
            command.AddOption(connectionStringEnvOption);
            command.AddOption(managementApiUrlOption);
            command.AddOption(managementApiUserNameOption);
            command.AddOption(managementApiPasswordOption);

            return new BrokerVerifierBinder(connectionStringOption, connectionStringEnvOption, managementApiUrlOption, managementApiUserNameOption, managementApiPasswordOption);
        }

        public static RoutingTopologyBinder CreateRoutingTopologyBinderWithOptions(Command command)
        {
            var routingTopologyTypeOption = CreateRoutingTopologyTypeOption();
            var useDurableEntitiesOption = CreateUseDurableEntitiesOption();
            var queueTypeOption = CreateQueueTypeOption();

            command.AddOption(routingTopologyTypeOption);
            command.AddOption(useDurableEntitiesOption);
            command.AddOption(queueTypeOption);

            return new RoutingTopologyBinder(routingTopologyTypeOption, useDurableEntitiesOption, queueTypeOption);
        }
    }
}
