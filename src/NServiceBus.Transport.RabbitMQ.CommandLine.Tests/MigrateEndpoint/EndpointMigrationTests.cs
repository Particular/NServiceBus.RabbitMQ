namespace NServiceBus.Transport.RabbitMQ.CommandLine.Tests.MigrateEndpoint
{
    using System;
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

        static string ConnectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString") ?? "host=localhost";
    }
}
