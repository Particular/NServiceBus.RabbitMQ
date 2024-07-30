
namespace NServiceBus.Transport.RabbitMQ
{
    using System.Threading;
    using Unicast.Messages;
    using System.Threading.Tasks;
    using Extensibility;

    class SubscriptionManager : ISubscriptionManager
    {
        readonly ConnectionFactory connectionFactory;
        readonly IRoutingTopology routingTopology;
        readonly string localQueue;

        public SubscriptionManager(ConnectionFactory connectionFactory, IRoutingTopology routingTopology, string localQueue)
        {
            this.connectionFactory = connectionFactory;
            this.routingTopology = routingTopology;
            this.localQueue = localQueue;
        }

        public async Task SubscribeAll(MessageMetadata[] eventTypes, ContextBag context, CancellationToken cancellationToken = default)
        {
            using var connection = await connectionFactory.CreateAdministrationConnection(cancellationToken).ConfigureAwait(false);
            using var channel = await connection.CreateChannelAsync(cancellationToken).ConfigureAwait(false);
            // TODO: Parallelize?
            foreach (var eventType in eventTypes)
            {
                await routingTopology.SetupSubscription(channel, eventType, localQueue, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task Unsubscribe(MessageMetadata eventType, ContextBag context, CancellationToken cancellationToken = default)
        {
            using var connection = await connectionFactory.CreateAdministrationConnection(cancellationToken).ConfigureAwait(false);
            using var channel = await connection.CreateChannelAsync(cancellationToken).ConfigureAwait(false);
            await routingTopology.TeardownSubscription(channel, eventType, localQueue, cancellationToken).ConfigureAwait(false);
        }
    }
}