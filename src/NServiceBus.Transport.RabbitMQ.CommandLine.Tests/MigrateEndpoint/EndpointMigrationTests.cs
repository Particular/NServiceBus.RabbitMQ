namespace NServiceBus.Transport.RabbitMQ.CommandLine.Tests.MigrateEndpoint
{
    using System;
    using System.Collections.Generic;
    using global::RabbitMQ.Client.Exceptions;
    using NUnit.Framework;

    [TestFixture]
    public class EndpointMigrationTests
    {
        [Test]
        public void Should_blow_up_when_endpoint_queue_does_not_exist()
        {
            var migrationCommand = new MigrateEndpointCommand();
            var endpointName = "NonExistingEndpoint";
            var ex = Assert.ThrowsAsync<OperationInterruptedException>(async () => await migrationCommand.Run(endpointName, ConnectionString, Topology.Conventional, true).ConfigureAwait(false));

#pragma warning disable CS8602 // Dereference of a possibly null reference.
            StringAssert.Contains(endpointName, ex.Message);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
        }

        [Test]
        public void Should_blow_up_when_endpoint_queue_already_is_quorum()
        {
            var migrationCommand = new MigrateEndpointCommand();
            var endpointName = "EndpointThatIsAlreadyMigrated";

            CreateQueue(endpointName, quorum: true);

            var ex = Assert.ThrowsAsync<Exception>(async () => await migrationCommand.Run(endpointName, ConnectionString, Topology.Conventional, true).ConfigureAwait(false));

#pragma warning disable CS8602 // Dereference of a possibly null reference.
            StringAssert.Contains(endpointName, ex.Message);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
        }

        void CreateQueue(string queueName, bool quorum)
        {
            CommandRunner.Run(ConnectionString, channel =>
            {
                var queueArguments = new Dictionary<string, object>();

                if (quorum)
                {
                    queueArguments.Add("x-queue-type", "quorum");
                }

                channel.QueueDeclare(queueName, true, false, false, queueArguments);
            });
        }

        static string ConnectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString") ?? "host=localhost";
    }
}
