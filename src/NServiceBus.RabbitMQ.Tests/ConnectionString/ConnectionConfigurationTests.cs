﻿namespace NServiceBus.Transports.RabbitMQ.Tests.ConnectionString
{
    using System;
    using Config;
    using NUnit.Framework;
    using Settings;

    [TestFixture]
    public class ConnectionConfigurationTests
    {
        [SetUp]
        public void Setup()
        {
            var settings = new SettingsHolder();
            settings.Set<NServiceBus.Routing.EndpointName>(new NServiceBus.Routing.EndpointName("endpoint"));

            parser = new ConnectionStringParser(settings);
            defaults = new ConnectionConfiguration();
        }

        ConnectionConfiguration defaults;
        ConnectionStringParser parser;
        string connectionString;
        ConnectionConfiguration connectionConfiguration;

        [Test]
        public void Should_default_the_port_if_not_set()
        {
            connectionString = ("host=myHost");
            connectionConfiguration = parser.Parse(connectionString);
            Assert.AreEqual(ConnectionConfiguration.DefaultPort, connectionConfiguration.Port);
        }

        [Test]
        public void Should_default_the_requested_heartbeat()
        {
            connectionString = ("host=localhost");
            connectionConfiguration = parser.Parse(connectionString);
            Assert.AreEqual(ConnectionConfiguration.DefaultHeartBeatInSeconds, connectionConfiguration.RequestedHeartbeat);
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

        [Test]
        public void Should_inform_that_dequeuetimeout_has_been_removed()
        {
            Exception exception = null;

            try
            {
                parser.Parse("host=localhost;dequeuetimeout=1");
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            Assert.IsNotNull(exception);
            Assert.That(exception.Message, Is.StringContaining("The 'DequeueTimeout' configuration setting has been removed"));
        }

        [Test]
        public void Should_inform_that_prefetchcount_has_been_removed()
        {
            Exception exception = null;

            try
            {
                parser.Parse("host=localhost;prefetchcount=100");
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            Assert.IsNotNull(exception);
            Assert.That(exception.Message, Is.StringContaining("The 'PrefetchCount' configuration setting has been removed"));
        }
    }
}
