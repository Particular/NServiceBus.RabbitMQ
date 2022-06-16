namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System.CommandLine;
    using global::RabbitMQ.Client.Exceptions;

    class EndpointCreateCommand
    {
        readonly ConnectionFactory connectionFactory;
        readonly IRoutingTopology routingTopology;
        readonly IConsole console;

        public static Command CreateCommand()
        {
            var command = new Command("create", "Creates queues and exchanges for an endpoint based on a routing topology");

            var endpointNameArgument = new Argument<string>(
                name: "endpointName",
                description: "Specifies the name of the endpoint to create");

            var errorQueueOption = new Option<string>(
                name: "--errorQueueName",
                description: "Specifies that an error queue with the specified name should be created");

            var auditQueueOption = new Option<string>(
                name: "--auditQueueName",
                description: "Specifies that an audit queue with the specified name should be created");

            var instanceDiscriminatorsOption = new Option<IEnumerable<string>>(
                name: "--instanceDiscriminators",
                description: "Specifies a list of instance discriminators to use when the endpoint needs uniquely addressable instances")
            {
                Arity = ArgumentArity.ZeroOrMore,
                AllowMultipleArgumentsPerToken = true
            };

            var connectionFactoryBinder = SharedOptions.CreateConnectionFactoryBinderWithOptions(command);
            var routingTopologyBinder = SharedOptions.CreateRoutingTopologyBinderWithOptions(command);

            command.AddArgument(endpointNameArgument);
            command.AddOption(errorQueueOption);
            command.AddOption(auditQueueOption);
            command.AddOption(instanceDiscriminatorsOption);

            command.SetHandler(async (string endpointName, string errorQueue, string auditQueue, IEnumerable<string> instanceDiscriminators, ConnectionFactory connectionFactory, IRoutingTopology routingTopology, IConsole console, CancellationToken cancellationToken) =>
            {
                var queueCreate = new EndpointCreateCommand(connectionFactory, routingTopology, console);
                await queueCreate.Run(endpointName, errorQueue, auditQueue, instanceDiscriminators, cancellationToken).ConfigureAwait(false);

            }, endpointNameArgument, errorQueueOption, auditQueueOption, instanceDiscriminatorsOption, connectionFactoryBinder, routingTopologyBinder);

            return command;
        }

        public EndpointCreateCommand(ConnectionFactory connectionFactory, IRoutingTopology routingTopology, IConsole console)
        {
            this.connectionFactory = connectionFactory;
            this.routingTopology = routingTopology;
            this.console = console;
        }

        public Task Run(string endpointName, string errorQueue, string auditQueue, IEnumerable<string> instanceDiscriminators, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;

            console.WriteLine("Connecting to broker");

            using var connection = connectionFactory.CreateAdministrationConnection();
            using var channel = connection.CreateModel();

            console.WriteLine("Checking for delay infrastructure v2");

            try
            {
                channel.ExchangeDeclarePassive(DelayInfrastructure.DeliveryExchange);
            }
            catch (OperationInterruptedException)
            {
                console.Error.Write("Fail: Delay infrastructure v2 not found.\n");
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

            routingTopology.Initialize(channel, receivingAddresses, sendingAddresses);

            foreach (var receivingAddress in receivingAddresses)
            {
                routingTopology.BindToDelayInfrastructure(channel, receivingAddress, DelayInfrastructure.DeliveryExchange, DelayInfrastructure.BindingKey(receivingAddress));
            }

            console.WriteLine($"Completed successfully");

            return Task.CompletedTask;
        }
    }
}
