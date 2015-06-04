namespace NServiceBus.Transports.RabbitMQ.Tests.ConnectionString
{
    using System;
    using Config;
    using NUnit.Framework;
    using Settings;

    [TestFixture]
    public class ConnectionConfigurationTests
    {
        #region Setup/Teardown

        [SetUp]
        public void Setup()
        {
            parser = new ConnectionStringParser(new SettingsHolder());
            defaults = new ConnectionConfiguration();
        }

        #endregion

        ConnectionConfiguration defaults;
        ConnectionStringParser parser;
        string connectionString;
        ConnectionConfiguration connectionConfiguration;

        [Test]
        public void Should_default_the_port_if_not_set()
        {
            connectionString = ("host=myHost");
            connectionConfiguration = parser.Parse(connectionString);
            Assert.AreEqual(ConnectionConfiguration.DefaultPort, connectionConfiguration.HostConfiguration.Port);
        }

        [Test]
        public void Should_not_default_the_prefetch_count()
        {
            connectionString = ("host=localhost");
            connectionConfiguration = parser.Parse(connectionString);
            Assert.AreEqual(0, connectionConfiguration.PrefetchCount);
        }

        [Test]
        public void Should_default_the_requested_heartbeat()
        {
            connectionString = ("host=localhost");
            connectionConfiguration = parser.Parse(connectionString);
            Assert.AreEqual(ConnectionConfiguration.DefaultHeartBeatInSeconds, connectionConfiguration.RequestedHeartbeat);
        }

        [Test]
        public void Should_default_the_dequeue_timeout()
        {
            connectionString = ("host=localhost");
            connectionConfiguration = parser.Parse(connectionString);
            Assert.AreEqual(ConnectionConfiguration.DefaultDequeueTimeout, connectionConfiguration.DequeueTimeout);
        }

        [Test]
        public void Should_set_default_password()
        {
            Assert.AreEqual(defaults.Password, "guest");
        }

        [Test]
        public void Should_set_default_port()
        {
            Assert.AreEqual(defaults.Port, 5672);
        }

        [Test]
        public void Should_set_default_username()
        {
            Assert.AreEqual(defaults.UserName, "guest");
        }

        [Test]
        public void Should_set_default_virtual_host()
        {
            Assert.AreEqual(defaults.VirtualHost, "/");
        }

        [Test]
        public void Should_inform_that_multiple_hosts_are_not_supported()
        {
            Exception exception = null;

            try
            {
                parser.Parse("host=localhost,host=localhost2");
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            Assert.IsNotNull(exception);
            Assert.That(exception.Message, Is.StringContaining("Multiple hosts are no longer supported"));
            Assert.That(exception.Message, Is.StringContaining("consider using a load balancer"));
        }
    }
}
