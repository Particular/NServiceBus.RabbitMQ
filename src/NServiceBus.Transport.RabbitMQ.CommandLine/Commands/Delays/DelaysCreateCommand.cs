namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System.CommandLine;
    using System.Threading;

    class DelaysCreateCommand(BrokerConnection brokerConnection, TextWriter output)
    {
        public static Command CreateCommand()
        {
            var command = new Command("create", "Create v2 delay infrastructure queues and exchanges");

            var brokerConnectionBinder = SharedOptions.CreateBrokerConnectionBinderWithOptions(command);

            command.SetAction(async (parseResult, cancellationToken) =>
            {
                var brokerConnection = brokerConnectionBinder.CreateBrokerConnection(parseResult);

                var delaysCreate = new DelaysCreateCommand(brokerConnection, parseResult.Configuration.Output);
                await delaysCreate.Run(cancellationToken);
            });

            return command;
        }

        public async Task Run(CancellationToken cancellationToken = default)
        {
            output.WriteLine($"Creating v2 delay infrastructure queues and exchanges...");

            using var connection = await brokerConnection.Create(cancellationToken);
            using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            await DelayInfrastructure.Build(channel, cancellationToken);

            output.WriteLine("Queues and exchanges created successfully");
        }
    }
}
