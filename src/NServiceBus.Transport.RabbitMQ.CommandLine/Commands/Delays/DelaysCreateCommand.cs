namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System.CommandLine;

    class DelaysCreateCommand
    {
        public static Command CreateCommand()
        {
            var command = new Command("create", "Create v2 delay infrastructure queues and exchanges");

            var brokerConnectionBinder = SharedOptions.CreateBrokerConnectionBinderWithOptions(command);

            command.SetHandler(async (brokerConnection, console, cancellationToken) =>
            {
                var delaysCreate = new DelaysCreateCommand(brokerConnection, console);
                await delaysCreate.Run(cancellationToken).ConfigureAwait(false);
            },
            brokerConnectionBinder, Bind.FromServiceProvider<IConsole>(), Bind.FromServiceProvider<CancellationToken>());

            return command;
        }

        public DelaysCreateCommand(BrokerConnection brokerConnection, IConsole console)
        {
            this.brokerConnection = brokerConnection;
            this.console = console;
        }

        public Task Run(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;

            console.WriteLine($"Creating v2 delay infrastructure queues and exchanges...");

            using var connection = brokerConnection.Create();
            using var channel = connection.CreateModel();

            DelayInfrastructure.Build(channel);

            console.WriteLine("Queues and exchanges created successfully");

            return Task.CompletedTask;
        }

        readonly BrokerConnection brokerConnection;
        readonly IConsole console;
    }
}
