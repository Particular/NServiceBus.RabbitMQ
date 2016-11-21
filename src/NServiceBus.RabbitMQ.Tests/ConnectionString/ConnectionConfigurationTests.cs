namespace NServiceBus.Transport.RabbitMQ.Tests.ConnectionString
{
    using System;
    using NUnit.Framework;
    using RabbitMQ;
    using Settings;

    [TestFixture]
    public class ConnectionConfigurationTests
    {
        SettingsHolder settings;

        [SetUp]
        public void Setup()
        {
            settings = new SettingsHolder();
            settings.Set("NServiceBus.Routing.EndpointName", "endpoint");

            DefaultConfigurationValues.Apply(settings);
        }

        [Test]
        public void Should_not_set_default_host()
        {
            Assert.IsFalse(settings.HasSetting(SettingsKeys.Host));
        }

        [Test]
        public void Should_set_default_port()
        {
            Assert.AreEqual(settings.Get(SettingsKeys.Port), 5672);
        }

        [Test]
        public void Should_set_default_virtual_host()
        {
            Assert.AreEqual(settings.Get(SettingsKeys.VirtualHost), "/");
        }

        [Test]
        public void Should_set_default_username()
        {
            Assert.AreEqual(settings.Get(SettingsKeys.UserName), "guest");
        }

        [Test]
        public void Should_set_default_password()
        {
            Assert.AreEqual(settings.Get(SettingsKeys.Password), "guest");
        }

        [Test]
        public void Should_set_default_requested_heartbeat()
        {
            Assert.AreEqual(settings.Get(SettingsKeys.RequestedHeartbeat), 5);
        }

        [Test]
        public void Should_set_default_retry_delay()
        {
            Assert.AreEqual(settings.Get(SettingsKeys.RetryDelay), TimeSpan.FromSeconds(10));
        }

        [Test]
        public void Should_set_default_use_tls()
        {
            Assert.AreEqual(settings.Get(SettingsKeys.UseTls), false);
        }

        [Test]
        public void Should_set_default_cert_path()
        {
            Assert.AreEqual(settings.Get(SettingsKeys.CertPath), "");
        }

        [Test]
        public void Should_set_default_retry_cert_passphrase()
        {
            Assert.AreEqual(settings.Get(SettingsKeys.CertPassphrase), string.Empty);
        }
    }
}
