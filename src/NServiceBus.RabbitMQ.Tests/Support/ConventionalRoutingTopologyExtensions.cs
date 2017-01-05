﻿using System.Collections.Generic;
using System.Linq;

namespace NServiceBus.Transport.RabbitMQ.Tests.Support
{
    static class ConventionalRoutingTopologyExtensions
    {
        public static void Reset(
            this ConventionalRoutingTopology routingTopology,
            ConnectionFactory connectionFactory,
            IList<string> receivingAddresses,
            IList<string> sendingAddresses)
        {
            using (var connection = connectionFactory.CreateAdministrationConnection())
            using (var channel = connection.CreateModel())
            {
                foreach (var address in receivingAddresses.Concat(sendingAddresses))
                {
                    channel.QueueDelete(address, false, false);
                    channel.ExchangeDelete(address, false);
                }

                routingTopology.DeclareAndInitialize(channel, receivingAddresses, sendingAddresses);
            }
        }
    }
}
