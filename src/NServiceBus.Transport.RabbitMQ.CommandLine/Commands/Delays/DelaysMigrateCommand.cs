namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System;
    using System.CommandLine;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;

    class DelaysMigrateCommand(BrokerConnection brokerConnection, IRoutingTopology routingTopology, TextWriter output)
    {
        const string poisonMessageQueue = "delays-migrate-poison-messages";

        public static Command CreateCommand()
        {
            var command = new Command("migrate", "Migrate in-flight delayed messages from the v1 delay infrastructure to the v2 delay infrastructure");

            var brokerConnectionBinder = SharedOptions.CreateBrokerConnectionBinderWithOptions(command);

            var routingTopologyTypeOption = SharedOptions.CreateRoutingTopologyTypeOption();
            command.Options.Add(routingTopologyTypeOption);

            command.SetAction(async (parseResult, cancellationToken) =>
            {
                var brokerConnection = brokerConnectionBinder.CreateBrokerConnection(parseResult);
                var routingTopologyType = parseResult.GetValue(routingTopologyTypeOption);

                IRoutingTopology routingTopology = routingTopologyType switch
                {
                    RoutingTopologyType.Conventional => new ConventionalRoutingTopology(true, QueueType.Quorum),
                    RoutingTopologyType.Direct => new DirectRoutingTopology(true, QueueType.Quorum),
                    _ => throw new InvalidOperationException()
                };

                var delaysMigrate = new DelaysMigrateCommand(brokerConnection, routingTopology, parseResult.InvocationConfiguration.Output);
                await delaysMigrate.Run(cancellationToken);
            });

            return command;
        }

        async Task Run(CancellationToken cancellationToken)
        {
            await using var connection = await brokerConnection.Create(cancellationToken);

            var createChannelOptions = new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true, outstandingPublisherConfirmationsRateLimiter: null);
            await using var channel = await connection.CreateChannelAsync(createChannelOptions, cancellationToken);

            await DelayCommandHelpers.TransferMessages(sourceIsV1: true, channel, channel, poisonMessageQueue, routingTopology, output, cancellationToken);
        }
    }
}
