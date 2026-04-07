namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System;
    using System.CommandLine;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;

    class DelaysTransferCommand(BrokerConnection sourceBrokerConnection, BrokerConnection destinationBrokerConnection, IRoutingTopology routingTopology, TextWriter output)
    {
        const string poisonMessageQueue = "delays-transfer-poison-messages";

        public static Command CreateCommand()
        {
            var command = new Command("transfer", "Transfer delayed messages from one broker to another");

            var (sourceBrokerConnectionBinder, destinationBrokerConnectionBinder) = SharedOptions.CreateSourceAndDestinationBrokerConnectionBindersWithOptions(command);

            var routingTopologyTypeOption = SharedOptions.CreateRoutingTopologyTypeOption();
            command.Options.Add(routingTopologyTypeOption);

            command.SetAction(async (parseResult, cancellationToken) =>
            {
                var sourceBrokerConnection = sourceBrokerConnectionBinder.CreateBrokerConnection(parseResult);
                var destinationBrokerConnection = destinationBrokerConnectionBinder.CreateBrokerConnection(parseResult);
                var routingTopologyType = parseResult.GetValue(routingTopologyTypeOption);

                IRoutingTopology routingTopology = routingTopologyType switch
                {
                    RoutingTopologyType.Conventional => new ConventionalRoutingTopology(true, QueueType.Quorum),
                    RoutingTopologyType.Direct => new DirectRoutingTopology(true, QueueType.Quorum),
                    _ => throw new InvalidOperationException()
                };

                var delaysTransfer = new DelaysTransferCommand(sourceBrokerConnection, destinationBrokerConnection, routingTopology, parseResult.InvocationConfiguration.Output);
                await delaysTransfer.Run(cancellationToken);
            });

            return command;
        }

        async Task Run(CancellationToken cancellationToken)
        {
            await using var sourceConnection = await sourceBrokerConnection.Create(cancellationToken);
            await using var destinationConnection = await destinationBrokerConnection.Create(cancellationToken);

            var createChannelOptions = new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true, outstandingPublisherConfirmationsRateLimiter: null);

            await using var sourceChannel = await sourceConnection.CreateChannelAsync(createChannelOptions, cancellationToken);
            await using var destinationChannel = await destinationConnection.CreateChannelAsync(createChannelOptions, cancellationToken);

            await DelayCommandHelpers.TransferMessages(sourceIsV1: false, sourceChannel, destinationChannel, poisonMessageQueue, routingTopology, output, cancellationToken);
        }
    }
}
