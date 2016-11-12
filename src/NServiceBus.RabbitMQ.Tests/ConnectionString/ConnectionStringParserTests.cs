﻿namespace NServiceBus.Transport.RabbitMQ.Tests.ConnectionString
{
    using System;
    using NUnit.Framework;
    using RabbitMQ;
    using Settings;

    [TestFixture]
    public class ConnectionStringParserTests
    {
        const string connectionString =
            "virtualHost=Copa;username=Copa;host=192.168.1.1:1234;password=abc_xyz;port=12345;requestedHeartbeat=3;" +
            "retryDelay=01:02:03;useTls=true;certPath=/path/to/client/keycert.p12;certPassPhrase=abc123";

        SettingsHolder settings;

        [SetUp]
        public void Setup()
        {
            settings = new SettingsHolder();
            settings.Set("NServiceBus.Routing.EndpointName", "endpoint");
        }

        [Test]
        public void Should_correctly_parse_full_connection_string()
        {
            var parser = new ConnectionStringParser(settings);
            parser.Parse(connectionString);

            Assert.AreEqual(settings.Get(SettingsKeys.Host), "192.168.1.1");
            Assert.AreEqual(settings.Get(SettingsKeys.Port), 1234);
            Assert.AreEqual(settings.Get(SettingsKeys.VirtualHost), "Copa");
            Assert.AreEqual(settings.Get(SettingsKeys.UserName), "Copa");
            Assert.AreEqual(settings.Get(SettingsKeys.Password), "abc_xyz");
            Assert.AreEqual(settings.Get(SettingsKeys.RequestedHeartbeat), 3);
            Assert.AreEqual(settings.Get(SettingsKeys.RetryDelay), new TimeSpan(1, 2, 3)); //01:02:03
            Assert.AreEqual(settings.Get(SettingsKeys.UseTls), true);
            Assert.AreEqual(settings.Get(SettingsKeys.CertPath), "/path/to/client/keycert.p12");
            Assert.AreEqual(settings.Get(SettingsKeys.CertPassphrase), "abc123");
        }

        [Test]
        public void Should_fail_if_host_is_not_present()
        {
            var parser = new ConnectionStringParser(settings);

            Assert.Throws<NotSupportedException>(() => parser.Parse("virtualHost=Copa;username=Copa;password=abc_xyz;port=12345;requestedHeartbeat=3"));
        }

        [Test]
        public void Should_parse_host()
        {
            var parser = new ConnectionStringParser(settings);
            parser.Parse("host=host.one:1001");

            Assert.AreEqual(settings.Get(SettingsKeys.Host), "host.one");
            Assert.AreEqual(settings.Get(SettingsKeys.Port), 1001);
        }

        [Test]
        public void Should_parse_host_with_separate_port()
        {
            var parser = new ConnectionStringParser(settings);
            parser.Parse("host=my.host.com;port=1234");

            Assert.AreEqual(settings.Get(SettingsKeys.Host), "my.host.com");
            Assert.AreEqual(settings.Get(SettingsKeys.Port), 1234);
        }

        [Test]
        public void Should_parse_host_without_port()
        {
            var parser = new ConnectionStringParser(settings);
            parser.Parse("host=my.host.com");

            Assert.AreEqual(settings.Get(SettingsKeys.Host), "my.host.com");
            Assert.AreEqual(settings.Get(SettingsKeys.Port), 5672);
        }

        [Test]
        public void Should_parse_the_hostname()
        {
            var parser = new ConnectionStringParser(settings);
            parser.Parse("host=myHost");

            Assert.AreEqual("myHost", settings.Get(SettingsKeys.Host));
        }

        [Test]
        public void Should_parse_the_password()
        {
            var parser = new ConnectionStringParser(settings);
            parser.Parse("host=localhost;password=test");

            Assert.AreEqual("test", settings.Get(SettingsKeys.Password));
        }

        [Test]
        public void Should_parse_the_port()
        {
            var parser = new ConnectionStringParser(settings);
            parser.Parse("host=localhost;port=8181");

            Assert.AreEqual(8181, settings.Get(SettingsKeys.Port));
        }

        [Test]
        public void Should_parse_the_requestedHeartbeat()
        {
            var parser = new ConnectionStringParser(settings);
            parser.Parse("host=localhost;requestedHeartbeat=5");

            Assert.AreEqual(5, settings.Get(SettingsKeys.RequestedHeartbeat));
        }

        [Test]
        public void Should_parse_the_retry_delay()
        {
            var parser = new ConnectionStringParser(settings);
            parser.Parse("host=localhost;retryDelay=00:00:10");

            Assert.AreEqual(TimeSpan.FromSeconds(10), settings.Get(SettingsKeys.RetryDelay));
        }

        [Test]
        public void Should_parse_the_username()
        {
            var parser = new ConnectionStringParser(settings);
            parser.Parse("host=localhost;username=test");

            Assert.AreEqual("test", settings.Get(SettingsKeys.UserName));
        }

        [Test]
        public void Should_parse_the_virtual_hostname()
        {
            var parser = new ConnectionStringParser(settings);
            parser.Parse("host=localhost;virtualHost=myVirtualHost");

            Assert.AreEqual("myVirtualHost", settings.Get(SettingsKeys.VirtualHost));
        }

        [Test]
        public void Should_parse_use_tls()
        {
            var parser = new ConnectionStringParser(settings);
            parser.Parse("host=localhost;useTls=true");

            Assert.AreEqual(true, settings.Get(SettingsKeys.UseTls));
            Assert.AreEqual(5671, settings.Get(SettingsKeys.Port));
        }

        [Test]
        public void Should_parse_the_cert_path()
        {
            var parser = new ConnectionStringParser(settings);
            parser.Parse("host=localhost;certPath=/path/keyfile.p12");

            Assert.AreEqual("/path/keyfile.p12", settings.Get(SettingsKeys.CertPath));
        }

        [Test]
        public void Should_parse_the_cert_passphrase()
        {
            var parser = new ConnectionStringParser(settings);
            parser.Parse("host=localhost;certPassphrase=abc123");

            Assert.AreEqual("abc123", settings.Get(SettingsKeys.CertPassphrase));
        }

        [Test]
        public void Should_throw_if_given_badly_formatted_retry_delay()
        {
            var parser = new ConnectionStringParser(settings);
            var formatException = Assert.Throws<FormatException>(
                () => parser.Parse("host=localhost;retryDelay=00:0d0:10"));

            Assert.AreEqual("00:0d0:10 is not a valid value for TimeSpan.", formatException.Message);
        }

        [Test]
        public void Should_throw_on_malformed_string()
        {
            var parser = new ConnectionStringParser(new SettingsHolder());

            Assert.Throws<ArgumentException>(() => parser.Parse("not a well formed name value pair;"));
        }

        [Test]
        public void Should_inform_that_multiple_hosts_are_not_supported()
        {
            var parser = new ConnectionStringParser(settings);

            var exception = Assert.Throws<NotSupportedException>(() => parser.Parse("host=localhost,host=localhost2"));

            Assert.That(exception.Message, Does.Contain("Multiple hosts are no longer supported"));
            Assert.That(exception.Message, Does.Contain("consider using a load balancer"));
        }

        [Test]
        public void Should_inform_that_dequeuetimeout_has_been_removed()
        {
            var parser = new ConnectionStringParser(settings);

            var exception = Assert.Throws<NotSupportedException>(() => parser.Parse("host=localhost;dequeuetimeout=1"));

            Assert.That(exception.Message, Does.Contain("The 'DequeueTimeout' connection string option has been removed"));
        }

        [Test]
        public void Should_inform_that_max_wait_time_for_confirmss_has_been_removed()
        {
            var parser = new ConnectionStringParser(settings);

            var exception = Assert.Throws<NotSupportedException>(() => parser.Parse("host=localhost;maxWaitTimeForConfirms=02:03:39"));

            Assert.That(exception.Message, Does.Contain("The 'MaxWaitTimeForConfirms' connection string option has been removed"));
        }

        [Test]
        public void Should_inform_that_prefetchcount_has_been_removed()
        {
            var parser = new ConnectionStringParser(settings);

            var exception = Assert.Throws<NotSupportedException>(() => parser.Parse("host=localhost;prefetchcount=100"));

            Assert.That(exception.Message, Does.Contain("The 'PrefetchCount' connection string option has been removed"));
        }

        [Test]
        public void Should_inform_that_usepublisherconfirms_has_been_removed()
        {
            var parser = new ConnectionStringParser(settings);

            var exception = Assert.Throws<NotSupportedException>(() => parser.Parse("host=localhost;usePublisherConfirms=true"));

            Assert.That(exception.Message, Does.Contain("The 'UsePublisherConfirms' connection string option has been removed"));
        }

        [Test]
        public void Should_list_all_invalid_options()
        {
            var parser = new ConnectionStringParser(settings);

            var exception = Assert.Throws<NotSupportedException>(() => parser.Parse("host=localhost,host=localhost2;usePublisherConfirms=true;prefetchcount=100;maxWaitTimeForConfirms=02:03:39;dequeuetimeout=1"));

            Assert.That(exception.Message, Does.Contain("Multiple hosts are no longer supported"));
            Assert.That(exception.Message, Does.Contain("consider using a load balancer"));
            Assert.That(exception.Message, Does.Contain("The 'UsePublisherConfirms' connection string option has been removed"));
            Assert.That(exception.Message, Does.Contain("The 'PrefetchCount' connection string option has been removed"));
            Assert.That(exception.Message, Does.Contain("The 'MaxWaitTimeForConfirms' connection string option has been removed"));
            Assert.That(exception.Message, Does.Contain("The 'DequeueTimeout' connection string option has been removed"));
        }
    }
}
