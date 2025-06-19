namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System.CommandLine;

    static class SharedOptions
    {
        const string ConnectionStringEnvironmentVariable = "RabbitMQTransport_ConnectionString";

        public static Option<string> CreateConnectionStringOption()
        {
            var connectionStringOption = new Option<string>("--connectionString", "-c")
            {
                Description = "Force this command to use the specified connection string"
            };

            return connectionStringOption;
        }

        public static Option<string> CreateConnectionStringEnvOption()
        {
            var connectionStringEnvOption = new Option<string>("--connectionStringEnv")
            {
                Description = "Specifies the environment variable where the connection string can be found. --connectionString, if specified, will take precedence over this option.",
                DefaultValueFactory = _ => ConnectionStringEnvironmentVariable
            };

            return connectionStringEnvOption;
        }

        public static Option<string> CreateManagementApiUrlOption()
        {
            var managementApiUrlOption = new Option<string>("--managementApiUrl")
            {
                Description = "Overrides the value inferred from the connection string"
            };

            return managementApiUrlOption;
        }

        public static Option<string> CreateManagementApiUserNameOption()
        {
            var managementApiUserNameOption = new Option<string>("--managementApiUserName")
            {
                Description = "Overrides the value inferred from the connection string. If provided, --managementApiPassword must also be provided or this option will be ignored."
            };

            return managementApiUserNameOption;
        }

        public static Option<string> CreateManagementApiPasswordOption()
        {
            var managementApiPasswordOption = new Option<string>("--managementApiPassword")
            {
                Description = "Overrides the value inferred from the connection string. If provided, --managementApiUserName must also be provided or this option will be ignored."
            };

            return managementApiPasswordOption;
        }

        public static Option<RoutingTopologyType> CreateRoutingTopologyTypeOption()
        {
            var routingTopologyTypeOption = new Option<RoutingTopologyType>("--routingTopology", "-r")
            {
                Description = "Specifies which routing topology to use.",
                DefaultValueFactory = _ => RoutingTopologyType.Conventional
            };

            return routingTopologyTypeOption;
        }

        public static Option<bool> CreateUseDurableEntitiesOption()
        {
            var useDurableEntities = new Option<bool>("--useDurableEntities", "-d")
            {
                Description = "Specifies if entities should be created as durable",
                DefaultValueFactory = _ => true
            };

            return useDurableEntities;
        }

        public static Option<QueueType> CreateQueueTypeOption()
        {
            var queueTypeOption = new Option<QueueType>("--queueType", "-t")
            {
                Description = "Specifies queue type will be used for queue creation",
                DefaultValueFactory = _ => QueueType.Quorum
            };

            return queueTypeOption;
        }

        public static Option<bool> CreateDisableCertValidationOption()
        {
            var disableCertValidationOption = new Option<bool>("--disableCertValidation")
            {
                Description = "Disable remote certificate validation when connecting to the broker",
                DefaultValueFactory = _ => false
            };

            return disableCertValidationOption;
        }

        public static Option<bool> CreateUseExternalAuthOption()
        {
            var useExternalAuthOption = new Option<bool>("--useExternalAuth")
            {
                Description = "Use the external authorization option when connecting to the broker",
                DefaultValueFactory = _ => false
            };

            return useExternalAuthOption;
        }

        public static Option<string> CreateCertPathOption()
        {
            var certPathOption = new Option<string>("--certPath")
            {
                Description = "Set the path to the client certificate file for connecting to the broker"
            };

            return certPathOption;
        }

        public static Option<string> CreateCertPassphraseOption()
        {
            var certPassphraseOption = new Option<string>("--certPassphrase")
            {
                Description = "The passphrase for client certificate file for when using a client certificate"
            };

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

            command.Options.Add(connectionStringOption);
            command.Options.Add(connectionStringEnvOption);
            command.Options.Add(managementApiUrlOption);
            command.Options.Add(managementApiUserNameOption);
            command.Options.Add(managementApiPasswordOption);
            command.Options.Add(certPathOption);
            command.Options.Add(certPassphraseOption);
            command.Options.Add(disableCertValidationOption);
            command.Options.Add(useExternalAuthOption);

            return new BrokerConnectionBinder(connectionStringOption, connectionStringEnvOption, managementApiUrlOption, managementApiUserNameOption, managementApiPasswordOption, certPathOption, certPassphraseOption, disableCertValidationOption, useExternalAuthOption);
        }

        public static BrokerVerifierBinder CreateBrokerVerifierBinderWithOptions(Command command)
        {
            var connectionStringOption = CreateConnectionStringOption();
            var connectionStringEnvOption = CreateConnectionStringEnvOption();
            var managementApiUrlOption = CreateManagementApiUrlOption();
            var managementApiUserNameOption = CreateManagementApiUserNameOption();
            var managementApiPasswordOption = CreateManagementApiPasswordOption();
            var disableCertValidationOption = CreateDisableCertValidationOption();

            command.Options.Add(connectionStringOption);
            command.Options.Add(connectionStringEnvOption);
            command.Options.Add(managementApiUrlOption);
            command.Options.Add(managementApiUserNameOption);
            command.Options.Add(managementApiPasswordOption);
            command.Options.Add(disableCertValidationOption);

            return new BrokerVerifierBinder(connectionStringOption, connectionStringEnvOption, managementApiUrlOption, managementApiUserNameOption, managementApiPasswordOption, disableCertValidationOption);
        }

        public static RoutingTopologyBinder CreateRoutingTopologyBinderWithOptions(Command command)
        {
            var routingTopologyTypeOption = CreateRoutingTopologyTypeOption();
            var useDurableEntitiesOption = CreateUseDurableEntitiesOption();
            var queueTypeOption = CreateQueueTypeOption();

            command.Options.Add(routingTopologyTypeOption);
            command.Options.Add(useDurableEntitiesOption);
            command.Options.Add(queueTypeOption);

            return new RoutingTopologyBinder(routingTopologyTypeOption, useDurableEntitiesOption, queueTypeOption);
        }
    }
}
