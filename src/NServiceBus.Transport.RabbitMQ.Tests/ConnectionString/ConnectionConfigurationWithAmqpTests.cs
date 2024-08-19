namespace NServiceBus.Transport.RabbitMQ.Tests.ConnectionString
{
    using System;
    using NUnit.Framework;
    using RabbitMQ;

    [TestFixture]
    public class ConnectionConfigurationWithAmqpTests
    {
        ConnectionConfiguration defaults = ConnectionConfiguration.Create("amqp://guest:guest@localhost:5672/");

        [Test]
        public void Should_correctly_parse_full_connection_string()
        {
            const string connectionString = "amqp://Copa:abc_xyz@192.168.1.1:5672/Copa";

            var connectionConfiguration = ConnectionConfiguration.Create(connectionString);

            Assert.That("192.168.1.1", Is.EqualTo(connectionConfiguration.Host));
            Assert.That(5672, Is.EqualTo(connectionConfiguration.Port));
            Assert.That("Copa", Is.EqualTo(connectionConfiguration.VirtualHost));
            Assert.That("Copa", Is.EqualTo(connectionConfiguration.UserName));
            Assert.That("abc_xyz", Is.EqualTo(connectionConfiguration.Password));
        }

        [Test]
        public void Should_fail_if_host_is_not_present()
        {
            Assert.Throws<UriFormatException>(() => ConnectionConfiguration.Create("amqp://:1234/"));
        }

        [TestCase("amqp", 5672U, false)]
        [TestCase("amqps", 5671U, true)]
        public void Should_determine_if_tls_should_be_used_from_connection_string(string scheme, uint port, bool useTls)
        {
            var connectionConfiguration = ConnectionConfiguration.Create($"{scheme}://guest:guest@localhost/");

            Assert.That(useTls, Is.EqualTo(connectionConfiguration.UseTls));
            Assert.That(port, Is.EqualTo(connectionConfiguration.Port));
        }

        [Test]
        public void Should_use_explicit_port_setting_over_scheme_default()
        {
            var connectionConfiguration = ConnectionConfiguration.Create("amqp://localhost:1234/");
            Assert.That(1234, Is.EqualTo(connectionConfiguration.Port));
        }

        [Test]
        public void Should_parse_host_without_port()
        {
            var connectionConfiguration = ConnectionConfiguration.Create("amqp://my.host.com/");

            Assert.That("my.host.com", Is.EqualTo(connectionConfiguration.Host));
            Assert.That(5672, Is.EqualTo(connectionConfiguration.Port));
        }

        [Test]
        public void Should_throw_on_invalid_port()
        {
            var connectionString = "amqp://localhost:notaport/";

            var exception = Assert.Throws<UriFormatException>(() => ConnectionConfiguration.Create(connectionString));

            Assert.That(exception.Message, Does.Contain("Invalid URI: Invalid port specified."));
        }

        [Test]
        public void Should_set_default_port()
        {
            Assert.That(5672, Is.EqualTo(defaults.Port));
        }

        [Test]
        public void Should_set_default_virtual_host()
        {
            Assert.That("/", Is.EqualTo(defaults.VirtualHost));
        }

        [Test]
        public void Should_set_default_username()
        {
            Assert.That("guest", Is.EqualTo(defaults.UserName));
        }

        [Test]
        public void Should_set_default_password()
        {
            Assert.That("guest", Is.EqualTo(defaults.Password));
        }

        [Test]
        public void Should_set_default_use_tls()
        {
            Assert.That(false, Is.EqualTo(defaults.UseTls));
        }
    }
}
