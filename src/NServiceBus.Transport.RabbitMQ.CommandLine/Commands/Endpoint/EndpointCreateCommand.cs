namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System.CommandLine;
    using global::RabbitMQ.Client.Exceptions;

    class EndpointCreateCommand(BrokerConnection brokerConnection, IRoutingTopology routingTopology, TextWriter output, TextWriter error)
    {
        public static Command CreateCommand()
        {
            var command = new Command("create", "Create queues and exchanges for an endpoint");

            var endpointNameArgument = new Argument<string>("endpointName")
            {
                Description = "The name of the endpoint to create"
            };

            var errorQueueOption = new Option<string>("--errorQueueName")
            {
                Description = "Also create an error queue with the specified name"
            };

            var auditQueueOption = new Option<string>("--auditQueueName")
            {
                Description = "Also create an audit queue with the specified name"
            };

            var instanceDiscriminatorsOption = new Option<IEnumerable<string>>("--instanceDiscriminators")
            {
                Arity = ArgumentArity.ZeroOrMore,
                AllowMultipleArgumentsPerToken = true,
                Description = "An optional list of instance discriminators to use when the endpoint needs uniquely addressable instances",
            };

            var brokerConnectionBinder = SharedOptions.CreateBrokerConnectionBinderWithOptions(command);
            var routingTopologyBinder = SharedOptions.CreateRoutingTopologyBinderWithOptions(command);

            command.Arguments.Add(endpointNameArgument);
            command.Options.Add(errorQueueOption);
            command.Options.Add(auditQueueOption);
            command.Options.Add(instanceDiscriminatorsOption);

            command.SetAction(async (parseResult, cancellationToken) =>
            {
                var brokerConnection = brokerConnectionBinder.CreateBrokerConnection(parseResult);
                var routingTopology = routingTopologyBinder.CreateRoutingTopology(parseResult);

                var queueCreate = new EndpointCreateCommand(brokerConnection, routingTopology, parseResult.InvocationConfiguration.Output, parseResult.InvocationConfiguration.Error);
                await queueCreate.Run(parseResult.GetRequiredValue(endpointNameArgument), parseResult.GetValue(errorQueueOption), parseResult.GetValue(auditQueueOption), parseResult.GetValue(instanceDiscriminatorsOption), cancellationToken);
            });

            return command;
        }

        public async Task Run(string endpointName, string? errorQueue, string? auditQueue, IEnumerable<string>? instanceDiscriminators, CancellationToken cancellationToken = default)
        {
            output.WriteLine("Connecting to broker");

            using var connection = await brokerConnection.Create(cancellationToken);
            using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            output.WriteLine("Checking for v2 delay infrastructure");

            try
            {
                await channel.ExchangeDeclarePassiveAsync(DelayInfrastructure.DeliveryExchange, cancellationToken);
            }
            catch (OperationInterruptedException)
            {
                error.Write("Fail: v2 delay infrastructure not found.\n");
                throw;
            }

            output.WriteLine($"Creating queues");

            var receivingAddresses = new List<string>() { endpointName };

            if (instanceDiscriminators is not null && instanceDiscriminators.Any())
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

            output.WriteLine($"Completed successfully");
        }
    }
}
