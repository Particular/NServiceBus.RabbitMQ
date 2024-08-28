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
                await delaysCreate.Run(cancellationToken);
            },
            brokerConnectionBinder, Bind.FromServiceProvider<IConsole>(), Bind.FromServiceProvider<CancellationToken>());

            return command;
        }

        public DelaysCreateCommand(BrokerConnection brokerConnection, IConsole console)
        {
            this.brokerConnection = brokerConnection;
            this.console = console;
        }

        public async Task Run(CancellationToken cancellationToken = default)
        {
            console.WriteLine($"Creating v2 delay infrastructure queues and exchanges...");

            using var connection = await brokerConnection.Create(cancellationToken);
            using var channel = await connection.CreateChannelAsync(cancellationToken);

            await DelayInfrastructure.Build(channel, cancellationToken);

            console.WriteLine("Queues and exchanges created successfully");
        }

        readonly BrokerConnection brokerConnection;
        readonly IConsole console;
    }
}
