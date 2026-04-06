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

        public static Option<string> CreateSourceConnectionStringOption()
        {
            var connectionStringOption = new Option<string>("--sourceConnectionString")
            {
                Description = "Force this command to use the specified connection string"
            };

            return connectionStringOption;
        }

        public static Option<string> CreateSourceConnectionStringEnvOption()
        {
            var connectionStringEnvOption = new Option<string>("--sourceConnectionStringEnv")
            {
                Description = "Specifies the environment variable where the connection string can be found. --connectionString, if specified, will take precedence over this option.",
                DefaultValueFactory = _ => "RabbitMQTransport_Source_ConnectionString"
            };

            return connectionStringEnvOption;
        }

        public static Option<string> CreateSourceManagementApiUrlOption()
        {
            var managementApiUrlOption = new Option<string>("--sourceManagementApiUrl")
            {
                Description = "Overrides the value inferred from the connection string"
            };

            return managementApiUrlOption;
        }

        public static Option<string> CreateSourceManagementApiUserNameOption()
        {
            var managementApiUserNameOption = new Option<string>("--sourceManagementApiUserName")
            {
                Description = "Overrides the value inferred from the connection string. If provided, --managementApiPassword must also be provided or this option will be ignored."
            };

            return managementApiUserNameOption;
        }

        public static Option<string> CreateSourceManagementApiPasswordOption()
        {
            var managementApiPasswordOption = new Option<string>("--sourceManagementApiPassword")
            {
                Description = "Overrides the value inferred from the connection string. If provided, --managementApiUserName must also be provided or this option will be ignored."
            };

            return managementApiPasswordOption;
        }

        public static Option<bool> CreateSourceUseExternalAuthOption()
        {
            var useExternalAuthOption = new Option<bool>("--sourceUseExternalAuth")
            {
                Description = "Use the external authorization option when connecting to the broker",
                DefaultValueFactory = _ => false
            };

            return useExternalAuthOption;
        }

        public static Option<string> CreateSourceCertPathOption()
        {
            var certPathOption = new Option<string>("--sourceCertPath")
            {
                Description = "Set the path to the client certificate file for connecting to the broker"
            };

            return certPathOption;
        }

        public static Option<string> CreateSourceCertPassphraseOption()
        {
            var certPassphraseOption = new Option<string>("--sourceCertPassphrase")
            {
                Description = "The passphrase for client certificate file for when using a client certificate"
            };

            return certPassphraseOption;
        }

        public static Option<string> CreateDestinationConnectionStringOption()
        {
            var connectionStringOption = new Option<string>("--destinationConnectionString")
            {
                Description = "Force this command to use the specified connection string"
            };

            return connectionStringOption;
        }

        public static Option<string> CreateDestinationConnectionStringEnvOption()
        {
            var connectionStringEnvOption = new Option<string>("--destinationConnectionStringEnv")
            {
                Description = "Specifies the environment variable where the connection string can be found. --connectionString, if specified, will take precedence over this option.",
                DefaultValueFactory = _ => "RabbitMQTransport_Destination_ConnectionString"
            };

            return connectionStringEnvOption;
        }

        public static Option<string> CreateDestinationManagementApiUrlOption()
        {
            var managementApiUrlOption = new Option<string>("--destinationManagementApiUrl")
            {
                Description = "Overrides the value inferred from the connection string"
            };

            return managementApiUrlOption;
        }

        public static Option<string> CreateDestinationManagementApiUserNameOption()
        {
            var managementApiUserNameOption = new Option<string>("--destinationManagementApiUserName")
            {
                Description = "Overrides the value inferred from the connection string. If provided, --managementApiPassword must also be provided or this option will be ignored."
            };

            return managementApiUserNameOption;
        }

        public static Option<string> CreateDestinationManagementApiPasswordOption()
        {
            var managementApiPasswordOption = new Option<string>("--destinationManagementApiPassword")
            {
                Description = "Overrides the value inferred from the connection string. If provided, --managementApiUserName must also be provided or this option will be ignored."
            };

            return managementApiPasswordOption;
        }

        public static Option<bool> CreateDestinationUseExternalAuthOption()
        {
            var useExternalAuthOption = new Option<bool>("--destinationUseExternalAuth")
            {
                Description = "Use the external authorization option when connecting to the broker",
                DefaultValueFactory = _ => false
            };

            return useExternalAuthOption;
        }

        public static Option<string> CreateDestinationCertPathOption()
        {
            var certPathOption = new Option<string>("--destinationCertPath")
            {
                Description = "Set the path to the client certificate file for connecting to the broker"
            };

            return certPathOption;
        }

        public static Option<string> CreateDestinationCertPassphraseOption()
        {
            var certPassphraseOption = new Option<string>("--destinationCertPassphrase")
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
            var useExternalAuthOption = CreateUseExternalAuthOption();
            var disableCertValidationOption = CreateDisableCertValidationOption();

            command.Options.Add(connectionStringOption);
            command.Options.Add(connectionStringEnvOption);
            command.Options.Add(managementApiUrlOption);
            command.Options.Add(managementApiUserNameOption);
            command.Options.Add(managementApiPasswordOption);
            command.Options.Add(certPathOption);
            command.Options.Add(certPassphraseOption);
            command.Options.Add(useExternalAuthOption);
            command.Options.Add(disableCertValidationOption);

            return new BrokerConnectionBinder(connectionStringOption, connectionStringEnvOption, managementApiUrlOption, managementApiUserNameOption, managementApiPasswordOption, certPathOption, certPassphraseOption, useExternalAuthOption, disableCertValidationOption);
        }

        public static (BrokerConnectionBinder Source, BrokerConnectionBinder Destination) CreateSourceAndDestinationBrokerConnectionBindersWithOptions(Command command)
        {
            var sourceConnectionStringOption = CreateSourceConnectionStringOption();
            var sourceConnectionStringEnvOption = CreateSourceConnectionStringEnvOption();
            var sourceManagementApiUrlOption = CreateSourceManagementApiUrlOption();
            var sourceManagementApiUserNameOption = CreateSourceManagementApiUserNameOption();
            var sourceManagementApiPasswordOption = CreateSourceManagementApiPasswordOption();
            var sourceCertPathOption = CreateSourceCertPathOption();
            var sourceCertPassphraseOption = CreateSourceCertPassphraseOption();
            var sourceUseExternalAuthOption = CreateSourceUseExternalAuthOption();

            var destinationConnectionStringOption = CreateDestinationConnectionStringOption();
            var destinationConnectionStringEnvOption = CreateDestinationConnectionStringEnvOption();
            var destinationManagementApiUrlOption = CreateDestinationManagementApiUrlOption();
            var destinationManagementApiUserNameOption = CreateDestinationManagementApiUserNameOption();
            var destinationManagementApiPasswordOption = CreateDestinationManagementApiPasswordOption();
            var destinationCertPathOption = CreateDestinationCertPathOption();
            var destinationCertPassphraseOption = CreateDestinationCertPassphraseOption();
            var destinationUseExternalAuthOption = CreateDestinationUseExternalAuthOption();

            var disableCertValidationOption = CreateDisableCertValidationOption();

            command.Options.Add(sourceConnectionStringOption);
            command.Options.Add(sourceConnectionStringEnvOption);
            command.Options.Add(sourceManagementApiUrlOption);
            command.Options.Add(sourceManagementApiUserNameOption);
            command.Options.Add(sourceManagementApiPasswordOption);
            command.Options.Add(sourceCertPathOption);
            command.Options.Add(sourceCertPassphraseOption);
            command.Options.Add(sourceUseExternalAuthOption);

            command.Options.Add(destinationConnectionStringOption);
            command.Options.Add(destinationConnectionStringEnvOption);
            command.Options.Add(destinationManagementApiUrlOption);
            command.Options.Add(destinationManagementApiUserNameOption);
            command.Options.Add(destinationManagementApiPasswordOption);
            command.Options.Add(destinationCertPathOption);
            command.Options.Add(destinationCertPassphraseOption);
            command.Options.Add(destinationUseExternalAuthOption);

            command.Options.Add(disableCertValidationOption);

            var sourceBrokerConnection = new BrokerConnectionBinder(sourceConnectionStringOption, sourceConnectionStringEnvOption, sourceManagementApiUrlOption, sourceManagementApiUserNameOption, sourceManagementApiPasswordOption,
                sourceCertPathOption, sourceCertPassphraseOption, sourceUseExternalAuthOption, disableCertValidationOption);

            var destinationBrokerConnection = new BrokerConnectionBinder(destinationConnectionStringOption, destinationConnectionStringEnvOption, destinationManagementApiUrlOption, destinationManagementApiUserNameOption, destinationManagementApiPasswordOption,
                destinationCertPathOption, destinationCertPassphraseOption, destinationUseExternalAuthOption, disableCertValidationOption);

            return (sourceBrokerConnection, destinationBrokerConnection);
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
