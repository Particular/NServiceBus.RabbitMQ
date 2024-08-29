namespace NServiceBus.Transport.RabbitMQ.Tests.Support
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    static class ConventionalRoutingTopologyExtensions
    {
        public static async Task ResetAsync(
            this ConventionalRoutingTopology routingTopology,
            ConnectionFactory connectionFactory,
            IEnumerable<string> receivingAddresses,
            IEnumerable<string> sendingAddresses)
        {
            using (var connection = await connectionFactory.CreateAdministrationConnection())
            using (var channel = await connection.CreateChannelAsync())
            {
                foreach (var address in receivingAddresses.Concat(sendingAddresses))
                {
                    await channel.QueueDeleteAsync(address, false, false);
                    await channel.ExchangeDeleteAsync(address, false);
                }

                await DelayInfrastructure.TearDown(channel);
                await DelayInfrastructure.Build(channel);

                await routingTopology.Initialize(channel, receivingAddresses, sendingAddresses);
            }
        }
    }
}
