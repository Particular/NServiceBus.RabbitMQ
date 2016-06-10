namespace NServiceBus.Transport.RabbitMQ.Tests.ConnectionString
{
    using System;
    using RabbitMQ;
    using NUnit.Framework;
    using Settings;

    [TestFixture]
    public class ConnectionStringParserTests
    {
        const string connectionString =
            "virtualHost=Copa;username=Copa;host=192.168.1.1:1234;password=abc_xyz;port=12345;requestedHeartbeat=3;" +
            "usePublisherConfirms=true;retryDelay=01:02:03;useTls=true;certPath=/path/to/client/keycert.p12;certPassPhrase=abc123";

        SettingsHolder settings;

        [SetUp]
        public void Setup()
        {
            settings = new SettingsHolder();
            settings.Set<Routing.EndpointName>(new Routing.EndpointName("endpoint"));
        }

        [Test]
        public void Should_correctly_parse_full_connection_string()
        {
            var parser = new ConnectionStringParser(settings);
            var connectionConfiguration = parser.Parse(connectionString);

            Assert.AreEqual(connectionConfiguration.Host, "192.168.1.1");
            Assert.AreEqual(connectionConfiguration.Port, 1234);
            Assert.AreEqual(connectionConfiguration.VirtualHost, "Copa");
            Assert.AreEqual(connectionConfiguration.UserName, "Copa");
            Assert.AreEqual(connectionConfiguration.Password, "abc_xyz");
            Assert.AreEqual(connectionConfiguration.RequestedHeartbeat, 3);
            Assert.AreEqual(connectionConfiguration.UsePublisherConfirms, true);
            Assert.AreEqual(connectionConfiguration.RetryDelay, new TimeSpan(1, 2, 3)); //01:02:03
            Assert.AreEqual(connectionConfiguration.UseTls, true);
            Assert.AreEqual(connectionConfiguration.CertPath, "/path/to/client/keycert.p12");
            Assert.AreEqual(connectionConfiguration.CertPassphrase, "abc123");
        }

        [Test]
        [ExpectedException(typeof(Exception))]
        public void Should_fail_if_host_is_not_present()
        {
            var parser = new ConnectionStringParser(settings);
            parser.Parse("virtualHost=Copa;username=Copa;password=abc_xyz;port=12345;requestedHeartbeat=3");
        }

        [Test]
        public void Should_parse_host()
        {
            var parser = new ConnectionStringParser(settings);
            var connectionConfiguration = parser.Parse("host=host.one:1001");

            Assert.AreEqual(connectionConfiguration.Host, "host.one");
            Assert.AreEqual(connectionConfiguration.Port, 1001);
        }

        [Test]
        public void Should_parse_host_with_separate_port()
        {
            var parser = new ConnectionStringParser(settings);
            var connectionConfiguration = parser.Parse("host=my.host.com;port=1234");

            Assert.AreEqual(connectionConfiguration.Host, "my.host.com");
            Assert.AreEqual(connectionConfiguration.Port, 1234);
        }

        [Test]
        public void Should_parse_host_without_port()
        {
            var parser = new ConnectionStringParser(settings);
            var connectionConfiguration = parser.Parse("host=my.host.com");

            Assert.AreEqual(connectionConfiguration.Host, "my.host.com");
            Assert.AreEqual(connectionConfiguration.Port, 5672);
        }

        [Test]
        public void Should_parse_the_hostname()
        {
            var parser = new ConnectionStringParser(settings);
            var connectionConfiguration = parser.Parse("host=myHost");

            Assert.AreEqual("myHost", connectionConfiguration.Host);
        }

        [Test]
        public void Should_parse_the_password()
        {
            var parser = new ConnectionStringParser(settings);
            var connectionConfiguration = parser.Parse("host=localhost;password=test");

            Assert.AreEqual("test", connectionConfiguration.Password);
        }

        [Test]
        public void Should_parse_the_port()
        {
            var parser = new ConnectionStringParser(settings);
            var connectionConfiguration = parser.Parse("host=localhost;port=8181");

            Assert.AreEqual(8181, connectionConfiguration.Port);
        }

        [Test]
        public void Should_parse_the_requestedHeartbeat()
        {
            var parser = new ConnectionStringParser(settings);
            var connectionConfiguration = parser.Parse("host=localhost;requestedHeartbeat=5");

            Assert.AreEqual(5, connectionConfiguration.RequestedHeartbeat);
        }

        [Test]
        public void Should_parse_the_retry_delay()
        {
            var parser = new ConnectionStringParser(settings);
            var connectionConfiguration = parser.Parse("host=localhost;retryDelay=00:00:10");

            Assert.AreEqual(TimeSpan.FromSeconds(10), connectionConfiguration.RetryDelay);
        }

        [Test]
        public void Should_parse_the_username()
        {
            var parser = new ConnectionStringParser(settings);
            var connectionConfiguration = parser.Parse("host=localhost;username=test");

            Assert.AreEqual("test", connectionConfiguration.UserName);
        }

        [Test]
        public void Should_parse_the_virtual_hostname()
        {
            var parser = new ConnectionStringParser(settings);
            var connectionConfiguration = parser.Parse("host=localhost;virtualHost=myVirtualHost");

            Assert.AreEqual("myVirtualHost", connectionConfiguration.VirtualHost);
        }

        [Test]
        public void Should_parse_use_tls()
        {
            var parser = new ConnectionStringParser(settings);
            var connectionConfiguration = parser.Parse("host=localhost;useTls=true");

            Assert.AreEqual(true, connectionConfiguration.UseTls);
            Assert.AreEqual(5671, connectionConfiguration.Port);
        }

        [Test]
        public void Should_parse_the_cert_path()
        {
            var parser = new ConnectionStringParser(settings);
            var connectionConfiguration = parser.Parse("host=localhost;certPath=/path/keyfile.p12");

            Assert.AreEqual("/path/keyfile.p12", connectionConfiguration.CertPath);
        }

        [Test]
        public void Should_parse_the_cert_passphrase()
        {
            var parser = new ConnectionStringParser(settings);
            var connectionConfiguration = parser.Parse("host=localhost;certPassphrase=abc123");

            Assert.AreEqual("abc123", connectionConfiguration.CertPassphrase);
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
        [ExpectedException(typeof(ArgumentException))]
        public void Should_throw_on_malformed_string()
        {
            var parser = new ConnectionStringParser(new SettingsHolder());
            parser.Parse("not a well formed name value pair;");
        }

        [Test]
        public void Should_inform_that_multiple_hosts_are_not_supported()
        {
            var parser = new ConnectionStringParser(settings);
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
            var parser = new ConnectionStringParser(settings);
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
            Assert.That(exception.Message, Is.StringContaining("The 'DequeueTimeout' connection string option has been removed"));
        }

        [Test]
        public void Should_inform_that_max_wait_time_for_confirmss_has_been_removed()
        {
            var parser = new ConnectionStringParser(settings);
            Exception exception = null;

            try
            {
                parser.Parse("host=localhost;maxWaitTimeForConfirms=02:03:39");
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            Assert.IsNotNull(exception);
            Assert.That(exception.Message, Is.StringContaining("The 'MaxWaitTimeForConfirms' connection string option has been removed"));
        }

        [Test]
        public void Should_inform_that_prefetchcount_has_been_removed()
        {
            var parser = new ConnectionStringParser(settings);
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
            Assert.That(exception.Message, Is.StringContaining("The 'PrefetchCount' connection string option has been removed"));
        }
    }
}
