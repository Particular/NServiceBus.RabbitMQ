namespace NServiceBus.Transport.RabbitMQ.Tests.Support
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    static class ConventionalRoutingTopologyExtensions
    {
        public static async Task ResetAsync(
            this ConventionalRoutingTopology routingTopology,
            ConnectionFactory connectionFactory,
            IEnumerable<string> receivingAddresses,
            IEnumerable<string> sendingAddresses,
            CancellationToken cancellationToken = default)
        {
            using (var connection = await connectionFactory.CreateAdministrationConnection(cancellationToken))
            using (var channel = await connection.CreateChannelAsync(cancellationToken))
            {
                foreach (var address in receivingAddresses.Concat(sendingAddresses))
                {
                    await channel.QueueDeleteAsync(address, false, false, cancellationToken: cancellationToken);
                    await channel.ExchangeDeleteAsync(address, false, cancellationToken: cancellationToken);
                }

                await DelayInfrastructure.TearDown(channel, cancellationToken);
                await DelayInfrastructure.Build(channel, cancellationToken);

                await routingTopology.Initialize(channel, receivingAddresses, sendingAddresses, cancellationToken);
            }
        }
    }
}
