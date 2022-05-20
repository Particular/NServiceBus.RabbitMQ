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
    }
}
