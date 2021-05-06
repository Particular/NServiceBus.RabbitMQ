﻿namespace NServiceBus.Transport.RabbitMQ.Tests.ConnectionString
{
    using NUnit.Framework;

    [TestFixture]
    public class ClusteredConnectionTests
    {
        RabbitMQClusterTransport CreateTransportDefinition(string connectionString)
        {
            return new RabbitMQClusterTransport(Topology.Conventional, connectionString, QueueMode.Classic, DelayedDeliverySupport.UnsafeEnabled);
        }

        [Test]
        public void Should_track_additional_hosts()
        {
            var connectionConfiguration = CreateTransportDefinition("host=host.one:1001;port=1002");
            connectionConfiguration.AddClusterNode("secondhost");

            Assert.AreEqual(1, connectionConfiguration.additionalHosts.Count);
            Assert.AreEqual("secondhost", connectionConfiguration.additionalHosts[0].Item1);
            Assert.AreEqual(-1, connectionConfiguration.additionalHosts[0].Item2);
        }
    }
}