namespace NServiceBus.Transport.RabbitMQ.Tests.ConnectionString
{
    using System;
    using System.IO;
    using NUnit.Framework;

    [TestFixture]
    public class ConnectionConfigurationTests
    {
        static readonly string certPath = $"{TestContext.CurrentContext.TestDirectory}{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}myp12.p12";

        RabbitMQTransport CreateTransportDefinition(string connectionString)
        {
            return new RabbitMQTransport(Topology.Conventional, connectionString);
        }

        [Test]
        public void Should_correctly_parse_full_connection_string()
        {
            var connectionString = $"virtualHost=Copa;username=Copa;host=192.168.1.1:1234;password=abc_xyz;port=12345;requestedHeartbeat=3;retryDelay=01:02:03;useTls=true;certPath={certPath};certPassPhrase=abc123";
            var connectionConfiguration = CreateTransportDefinition(connectionString);

            Assert.AreEqual(connectionConfiguration.Host, "192.168.1.1");
            Assert.AreEqual(connectionConfiguration.Port, 1234);
            Assert.AreEqual(connectionConfiguration.VHost, "Copa");
            Assert.AreEqual(connectionConfiguration.UserName, "Copa");
            Assert.AreEqual(connectionConfiguration.Password, "abc_xyz");
            Assert.AreEqual(connectionConfiguration.HeartbeatInterval, TimeSpan.FromSeconds(3));
            Assert.AreEqual(connectionConfiguration.NetworkRecoveryInterval, new TimeSpan(1, 2, 3)); //01:02:03
            Assert.AreEqual("O=Particular, S=Some-State, C=PL", connectionConfiguration.ClientCertificate.Issuer);
        }

        [Test]
        public void Should_fail_if_host_is_not_present()
        {
            Assert.Throws<Exception>(() => CreateTransportDefinition("virtualHost=Copa;username=Copa;password=abc_xyz;port=12345;requestedHeartbeat=3"));
        }

        [Test]
        public void Should_parse_host()
        {
            var connectionConfiguration = CreateTransportDefinition("host=host.one:1001;port=1002");

            Assert.AreEqual(connectionConfiguration.Host, "host.one");
            Assert.AreEqual(connectionConfiguration.Port, 1001);
        }

        [Test]
        public void Should_parse_host_with_separate_port()
        {
            var connectionConfiguration = CreateTransportDefinition("host=my.host.com;port=1234");

            Assert.AreEqual(connectionConfiguration.Host, "my.host.com");
            Assert.AreEqual(connectionConfiguration.Port, 1234);
        }

        [Test]
        public void Should_parse_host_without_port()
        {
            var connectionConfiguration = CreateTransportDefinition("host=my.host.com");

            Assert.AreEqual(connectionConfiguration.Host, "my.host.com");
        }

        [Test]
        public void Should_parse_the_hostname()
        {
            var connectionConfiguration = CreateTransportDefinition("host=myHost");

            Assert.AreEqual("myHost", connectionConfiguration.Host);
        }

        [Test]
        public void Should_parse_the_password()
        {
            var connectionConfiguration = CreateTransportDefinition("host=localhost;password=test");

            Assert.AreEqual("test", connectionConfiguration.Password);
        }

        [Test]
        public void Should_parse_the_port()
        {
            var connectionConfiguration = CreateTransportDefinition("host=localhost;port=8181");

            Assert.AreEqual(8181, connectionConfiguration.Port);
        }

        [Test]
        public void Should_parse_the_requestedHeartbeat()
        {
            var connectionConfiguration = CreateTransportDefinition("host=localhost;requestedHeartbeat=5");

            Assert.AreEqual(TimeSpan.FromSeconds(5), connectionConfiguration.HeartbeatInterval);
        }

        [Test]
        public void Should_parse_the_retry_delay()
        {
            var connectionConfiguration = CreateTransportDefinition("host=localhost;retryDelay=00:00:10");

            Assert.AreEqual(TimeSpan.FromSeconds(10), connectionConfiguration.NetworkRecoveryInterval);
        }

        [Test]
        public void Should_parse_the_username()
        {
            var connectionConfiguration = CreateTransportDefinition("host=localhost;username=test");

            Assert.AreEqual("test", connectionConfiguration.UserName);
        }

        [Test]
        public void Should_parse_the_virtual_hostname()
        {
            var connectionConfiguration = CreateTransportDefinition("host=localhost;virtualHost=myVirtualHost");

            Assert.AreEqual("myVirtualHost", connectionConfiguration.VHost);
        }

        [Test]
        public void Should_parse_use_tls()
        {
            var connectionConfiguration = CreateTransportDefinition("host=localhost;useTls=true");

            Assert.AreEqual(true, connectionConfiguration.UseTLS);
        }
        [Test]
        public void Should_parse_the_cert_path()
        {
            var connectionConfiguration = CreateTransportDefinition($"host=localhost;certPath={certPath};certPassphrase=abc123");

            Assert.AreEqual("O=Particular, S=Some-State, C=PL", connectionConfiguration.ClientCertificate.Issuer);
        }

        [Test]
        public void Should_throw_on_malformed_string()
        {
            Assert.Throws<ArgumentException>(() => CreateTransportDefinition("not a well formed name value pair;"));
        }
    }
}
