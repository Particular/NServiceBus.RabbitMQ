namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System.CommandLine;
    using global::RabbitMQ.Client.Exceptions;

    class EndpointCreateCommand
    {
        readonly BrokerConnection brokerConnection;
        readonly IRoutingTopology routingTopology;
        readonly IConsole console;

        public static Command CreateCommand()
        {
            var command = new Command("create", "Create queues and exchanges for an endpoint");

            var endpointNameArgument = new Argument<string>(
                name: "endpointName",
                description: "The name of the endpoint to create");

            var errorQueueOption = new Option<string>(
                name: "--errorQueueName",
                description: "Also create an error queue with the specified name");

            var auditQueueOption = new Option<string>(
                name: "--auditQueueName",
                description: "Also create an audit queue with the specified name");

            var instanceDiscriminatorsOption = new Option<IEnumerable<string>>(
                name: "--instanceDiscriminators",
                description: "An optional list of instance discriminators to use when the endpoint needs uniquely addressable instances")
            {
                Arity = ArgumentArity.ZeroOrMore,
                AllowMultipleArgumentsPerToken = true
            };

            var brokerConnectionBinder = SharedOptions.CreateBrokerConnectionBinderWithOptions(command);
            var routingTopologyBinder = SharedOptions.CreateRoutingTopologyBinderWithOptions(command);

            command.AddArgument(endpointNameArgument);
            command.AddOption(errorQueueOption);
            command.AddOption(auditQueueOption);
            command.AddOption(instanceDiscriminatorsOption);

            command.SetHandler(async (endpointName, errorQueue, auditQueue, instanceDiscriminators, brokerConnection, routingTopology, console, cancellationToken) =>
            {
                var queueCreate = new EndpointCreateCommand(brokerConnection, routingTopology, console);
                await queueCreate.Run(endpointName, errorQueue, auditQueue, instanceDiscriminators, cancellationToken);
            },
            endpointNameArgument, errorQueueOption, auditQueueOption, instanceDiscriminatorsOption, brokerConnectionBinder, routingTopologyBinder, Bind.FromServiceProvider<IConsole>(), Bind.FromServiceProvider<CancellationToken>());

            return command;
        }

        public EndpointCreateCommand(BrokerConnection brokerConnection, IRoutingTopology routingTopology, IConsole console)
        {
            this.brokerConnection = brokerConnection;
            this.routingTopology = routingTopology;
            this.console = console;
        }

        public async Task Run(string endpointName, string? errorQueue, string? auditQueue, IEnumerable<string> instanceDiscriminators, CancellationToken cancellationToken = default)
        {
            console.WriteLine("Connecting to broker");

            using var connection = await brokerConnection.Create(cancellationToken);
            using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            console.WriteLine("Checking for v2 delay infrastructure");

            try
            {
                await channel.ExchangeDeclarePassiveAsync(DelayInfrastructure.DeliveryExchange, cancellationToken);
            }
            catch (OperationInterruptedException)
            {
                console.Error.Write("Fail: v2 delay infrastructure not found.\n");
                throw;
            }

            console.WriteLine($"Creating queues");

            var receivingAddresses = new List<string>() { endpointName };

            if (instanceDiscriminators.Any())
            {
                receivingAddresses.AddRange(instanceDiscriminators.Select(discriminator => $"{endpointName}-{discriminator}"));
            }

            var sendingAddresses = new List<string>();

            if (!string.IsNullOrWhiteSpace(errorQueue))
            {
                sendingAddresses.Add(errorQueue);
            }

            if (!string.IsNullOrWhiteSpace(auditQueue))
            {
                sendingAddresses.Add(auditQueue);
            }

            await routingTopology.Initialize(channel, receivingAddresses, sendingAddresses, cancellationToken);

            foreach (var receivingAddress in receivingAddresses)
            {
                await routingTopology.BindToDelayInfrastructure(channel, receivingAddress, DelayInfrastructure.DeliveryExchange, DelayInfrastructure.BindingKey(receivingAddress), cancellationToken);
            }

            console.WriteLine($"Completed successfully");
        }
    }
}
