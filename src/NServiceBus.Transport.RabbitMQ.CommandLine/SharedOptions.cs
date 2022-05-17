namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System.CommandLine;

    static class SharedOptions
    {
        const string ConnectionStringEnvironmentVariable = "RabbitMQTransport_ConnectionString";

        static Option<string>? ConnectionStringOption = null;

        public static Option<string> ConnectionString
        {
            get
            {
                if (ConnectionStringOption == null)
                {
                    ConnectionStringOption = new Option<string>(
                        name: "--connectionString",
                        description: $"Overrides environment variable '{ConnectionStringEnvironmentVariable}'",
                        getDefaultValue: () => Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable) ?? string.Empty);

                    ConnectionStringOption.AddAlias("-c");
                }

                return ConnectionStringOption;
            }
        }
    }
}
