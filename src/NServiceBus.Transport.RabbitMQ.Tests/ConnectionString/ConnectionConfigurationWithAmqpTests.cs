namespace NServiceBus.Transport.RabbitMQ.Tests.ConnectionString
{
    using System;
    using NUnit.Framework;

    [TestFixture]
    public class ConnectionConfigurationWithAmqpTests
    {
        RabbitMQTransport CreateTransportDefinition(string connectionString)
        {
            return new RabbitMQTransport(Topology.Conventional, connectionString, QueueType.Classic);
        }

        [Test]
        public void Should_correctly_parse_full_connection_string()
        {
            const string connectionString = "amqp://Copa:abc_xyz@192.168.1.1:5672/Copa";

            var connectionConfiguration = CreateTransportDefinition(connectionString);

            Assert.AreEqual(connectionConfiguration.Host, "192.168.1.1");
            Assert.AreEqual(connectionConfiguration.Port, 5672);
            Assert.AreEqual(connectionConfiguration.VHost, "Copa");
            Assert.AreEqual(connectionConfiguration.UserName, "Copa");
            Assert.AreEqual(connectionConfiguration.Password, "abc_xyz");
        }

        [Test]
        public void Should_fail_if_host_is_not_present()
        {
            Assert.Throws<UriFormatException>(() => CreateTransportDefinition("amqp://:1234/"));
        }

        [TestCase("amqp", 5672U, false)]
        [TestCase("amqps", 5671U, true)]
        public void Should_determine_if_tls_should_be_used_from_connection_string(string scheme, uint port, bool useTls)
        {
            var connectionConfiguration = CreateTransportDefinition($"{scheme}://guest:guest@localhost/");

            Assert.AreEqual(connectionConfiguration.UseTLS, useTls);
        }

        [Test]
        public void Should_use_explicit_port_setting_over_scheme_default()
        {
            var connectionConfiguration = CreateTransportDefinition("amqp://localhost:1234/");
            Assert.AreEqual(connectionConfiguration.Port, 1234);
        }

        [Test]
        public void Should_parse_host_without_port()
        {
            var connectionConfiguration = CreateTransportDefinition("amqp://my.host.com/");

            Assert.AreEqual(connectionConfiguration.Host, "my.host.com");
        }

        [Test]
        public void Should_throw_on_invalid_port()
        {
            var connectionString = "amqp://localhost:notaport/";

            var exception = Assert.Throws<UriFormatException>(() => CreateTransportDefinition(connectionString));

            Assert.That(exception.Message, Does.Contain("Invalid URI: Invalid port specified."));
        }
    }
}
