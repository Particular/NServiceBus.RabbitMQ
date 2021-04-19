namespace NServiceBus.Transport.RabbitMQ.Tests.Support
{
    using System.Collections.Generic;
    using System.Linq;

    static class ConventionalRoutingTopologyExtensions
    {
        public static void Reset(
            this ConventionalRoutingTopology routingTopology,
            ConnectionFactory connectionFactory,
            IEnumerable<string> receivingAddresses,
            IEnumerable<string> sendingAddresses,
            bool isEndpointQuorum)
        {
            using (var connection = connectionFactory.CreateAdministrationConnection())
            using (var channel = connection.CreateModel())
            {
                foreach (var address in receivingAddresses.Concat(sendingAddresses))
                {
                    channel.QueueDelete(address, false, false);
                    channel.ExchangeDelete(address, false);
                }

                DelayInfrastructure.TearDown(channel);
                DelayInfrastructure.Build(channel);

                routingTopology.Initialize(connection, receivingAddresses, sendingAddresses, isEndpointQuorum, false);
            }
        }
    }
}
