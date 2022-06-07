namespace NServiceBus.Transport.RabbitMQ.CommandLine.Commands.Endpoint
{
    using System.CommandLine;

    class QueueCreateCommand
    {
        readonly ConnectionFactory connectionFactory;
        readonly IRoutingTopology routingTopology;
        readonly IConsole console;

        public static Command CreateCommand()
        {
            var command = new Command("create", "Create standard queues and exchanges based on a routing topology");

            var queueNameArgument = new Argument<string>(
                name: "queueName",
                description: "The name of the queue to be created");

            var errorQueueOption = new Option<string>(
                name: "--createErrorQueue",
                description: "Declare an error queue with the specified name");

            var auditQueueOption = new Option<string>(
                name: "--createAuditQueue",
                description: "Declare an audit queue with the specified name");

            var connectionFactoryBinder = SharedOptions.CreateConnectionFactoryBinderWithOptions(command);
            var routingTopologyBinder = SharedOptions.CreateRoutingTopologyBinderWithOptions(command);

            command.AddArgument(queueNameArgument);
            command.AddOption(errorQueueOption);
            command.AddOption(auditQueueOption);

            command.SetHandler(async (string queueName, string? errorQueue, string? auditQueue, ConnectionFactory connectionFactory, IRoutingTopology routingTopology, IConsole console, CancellationToken cancellationToken) =>
            {
                var queueCreate = new QueueCreateCommand(connectionFactory, routingTopology, console);
                await queueCreate.Run(queueName, errorQueue, auditQueue, cancellationToken).ConfigureAwait(false);

            }, queueNameArgument, errorQueueOption, auditQueueOption, connectionFactoryBinder, routingTopologyBinder);

            return command;
        }

        public QueueCreateCommand(ConnectionFactory connectionFactory, IRoutingTopology routingTopology, IConsole console)
        {
            this.connectionFactory = connectionFactory;
            this.routingTopology = routingTopology;
            this.console = console;
        }

        public Task Run(string queueName, string? errorQueue, string? auditQueue, CancellationToken cancellationToken = default)
        {
            console.WriteLine($"Creating queues..");

            using var connection = connectionFactory.CreateAdministrationConnection();
            using var channel = connection.CreateModel();

            var sendingAddresses = new List<string>();

            if (!string.IsNullOrWhiteSpace(errorQueue))
            {
                sendingAddresses.Add(errorQueue);
            }

            if (!string.IsNullOrWhiteSpace(auditQueue))
            {
                sendingAddresses.Add(auditQueue);
            }

            routingTopology.Initialize(channel, new string[] { queueName }, sendingAddresses);
            routingTopology.BindToDelayInfrastructure(channel, queueName, DelayInfrastructure.DeliveryExchange, DelayInfrastructure.BindingKey(queueName));

            console.WriteLine($"Completed successfully");

            return Task.CompletedTask;
        }
    }
}
