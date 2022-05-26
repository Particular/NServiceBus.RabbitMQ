namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System.CommandLine;

    class DelaysCreateCommand
    {
        public static Command CreateCommand()
        {
            var command = new Command("create", "Create v2 delay infrastructure queues and exchanges");

            var connectionFactoryBinder = SharedOptions.CreateConnectionFactoryBinderWithOptions(command);

            command.SetHandler(async (ConnectionFactory connectionFactory, IConsole console, CancellationToken cancellationToken) =>
            {
                var createProcess = new DelaysCreateCommand(connectionFactory, console);
                await createProcess.Run(cancellationToken).ConfigureAwait(false);

            }, connectionFactoryBinder);

            return command;
        }

        public DelaysCreateCommand(ConnectionFactory connectionFactory, IConsole console)
        {
            this.connectionFactory = connectionFactory;
            this.console = console;
        }

        public Task Run(CancellationToken cancellationToken = default)
        {
            using (var connection = connectionFactory.CreateAdministrationConnection())
            {
                using (var channel = connection.CreateModel())
                {
                    try
                    {
                        DelayInfrastructure.Build(channel);
                        channel.Close();

                        console.WriteLine("Delay infrastructure v2 created successfully");
                    }
                    catch (Exception ex)
                    {
                        console.WriteLine($"Fail: {ex.Message}");
                    }
                    finally
                    {
                        if (channel.IsOpen)
                        {
                            channel.Close();
                        }
                    }
                }

                if (connection.IsOpen)
                {
                    connection.Close();
                }
            }

            return Task.CompletedTask;
        }

        readonly ConnectionFactory connectionFactory;
        readonly IConsole console;
    }
}
