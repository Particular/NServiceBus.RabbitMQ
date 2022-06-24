namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System.CommandLine;

    class DelaysCreateCommand
    {
        public static Command CreateCommand()
        {
            var command = new Command("create", "Create delay infrastructure v2 queues and exchanges");

            var connectionFactoryBinder = SharedOptions.CreateConnectionFactoryBinderWithOptions(command);

            command.SetHandler(async (connectionFactory, console, cancellationToken) =>
            {
                var delaysCreate = new DelaysCreateCommand(connectionFactory, console);
                await delaysCreate.Run(cancellationToken).ConfigureAwait(false);
            },
            connectionFactoryBinder, Bind.FromServiceProvider<IConsole>(), Bind.FromServiceProvider<CancellationToken>());

            return command;
        }

        public DelaysCreateCommand(ConnectionFactory connectionFactory, IConsole console)
        {
            this.connectionFactory = connectionFactory;
            this.console = console;
        }

        public Task Run(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;

            console.WriteLine($"Creating delay infrastructure v2..");

            using var connection = connectionFactory.CreateAdministrationConnection();
            using var channel = connection.CreateModel();

            DelayInfrastructure.Build(channel);

            console.WriteLine("Delay infrastructure v2 created successfully");

            return Task.CompletedTask;
        }

        readonly ConnectionFactory connectionFactory;
        readonly IConsole console;
    }
}
