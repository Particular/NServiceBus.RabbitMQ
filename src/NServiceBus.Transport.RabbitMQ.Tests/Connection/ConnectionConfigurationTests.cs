namespace NServiceBus.Transport.RabbitMQ.Tests.ConnectionString
{
    using System;
    using NUnit.Framework;
    using RabbitMQ;

    [TestFixture]
    public class ConnectionConfigurationTests
    {
        const string connectionString = "virtualHost=Copa;username=Copa;host=192.168.1.1:1234;password=abc_xyz;port=12345;useTls=true";

        ConnectionConfiguration defaults = ConnectionConfiguration.Create("host=localhost");

        [Test]
        public void Should_correctly_parse_full_connection_string()
        {
            var connectionConfiguration = ConnectionConfiguration.Create(connectionString);

            Assert.That(connectionConfiguration.Host, Is.EqualTo("192.168.1.1"));
            Assert.That(connectionConfiguration.Port, Is.EqualTo(1234));
            Assert.That(connectionConfiguration.VirtualHost, Is.EqualTo("Copa"));
            Assert.That(connectionConfiguration.UserName, Is.EqualTo("Copa"));
            Assert.That(connectionConfiguration.Password, Is.EqualTo("abc_xyz"));
            Assert.That(connectionConfiguration.UseTls, Is.True);
        }

        [Test]
        public void Should_fail_if_host_is_not_present()
        {
            Assert.Throws<NotSupportedException>(() => ConnectionConfiguration.Create("virtualHost=Copa;username=Copa;password=abc_xyz;port=12345;requestedHeartbeat=3"));
        }

        [Test]
        public void Should_parse_host()
        {
            var connectionConfiguration = ConnectionConfiguration.Create("host=host.one:1001;port=1002");

            Assert.That(connectionConfiguration.Host, Is.EqualTo("host.one"));
            Assert.That(connectionConfiguration.Port, Is.EqualTo(1001));
        }

        [Test]
        public void Should_parse_host_with_separate_port()
        {
            var connectionConfiguration = ConnectionConfiguration.Create("host=my.host.com;port=1234");

            Assert.That(connectionConfiguration.Host, Is.EqualTo("my.host.com"));
            Assert.That(connectionConfiguration.Port, Is.EqualTo(1234));
        }

        [Test]
        public void Should_parse_host_without_port()
        {
            var connectionConfiguration = ConnectionConfiguration.Create("host=my.host.com");

            Assert.That(connectionConfiguration.Host, Is.EqualTo("my.host.com"));
            Assert.That(connectionConfiguration.Port, Is.EqualTo(5672));
        }

        [Test]
        public void Should_parse_the_hostname()
        {
            var connectionConfiguration = ConnectionConfiguration.Create("host=myHost");

            Assert.That(connectionConfiguration.Host, Is.EqualTo("myHost"));
        }

        [Test]
        public void Should_parse_the_password()
        {
            var connectionConfiguration = ConnectionConfiguration.Create("host=localhost;password=test");

            Assert.That(connectionConfiguration.Password, Is.EqualTo("test"));
        }

        [Test]
        public void Should_parse_the_port()
        {
            var connectionConfiguration = ConnectionConfiguration.Create("host=localhost;port=8181");

            Assert.That(connectionConfiguration.Port, Is.EqualTo(8181));
        }

        [Test]
        public void Should_parse_the_username()
        {
            var connectionConfiguration = ConnectionConfiguration.Create("host=localhost;username=test");

            Assert.That(connectionConfiguration.UserName, Is.EqualTo("test"));
        }

        [Test]
        public void Should_parse_the_virtual_hostname()
        {
            var connectionConfiguration = ConnectionConfiguration.Create("host=localhost;virtualHost=myVirtualHost");

            Assert.That(connectionConfiguration.VirtualHost, Is.EqualTo("myVirtualHost"));
        }

        [Test]
        public void Should_parse_use_tls()
        {
            var connectionConfiguration = ConnectionConfiguration.Create("host=localhost;useTls=true");

            Assert.That(connectionConfiguration.UseTls, Is.True);
            Assert.That(connectionConfiguration.Port, Is.EqualTo(5671));
        }

        [Test]
        public void Should_throw_on_malformed_string()
        {
            Assert.Throws<ArgumentException>(() => ConnectionConfiguration.Create("not a well formed name value pair;"));
        }

        [Test]
        public void Should_list_all_invalid_options()
        {
            var connectionString =
                "host=:notaport1,host=localhost2;" +
                "port=notaport2;" +
                "useTls=notusetls;" +
                "requestedHeartbeat=60;" +
                "retryDelay=10;" +
                "usePublisherConfirms=true;" +
                "prefetchcount=100;" +
                "maxWaitTimeForConfirms=02:03:39;" +
                "dequeuetimeout=1;" +
                "certPath =/path/to/client/keycert.p12;" +
                "certPassPhrase = abc123;";

            var exception = Assert.Throws<NotSupportedException>(() =>
                ConnectionConfiguration.Create(connectionString));

            Assert.That(exception.Message, Does.Contain("Multiple hosts are no longer supported"));
            Assert.That(exception.Message, Does.Contain("Empty host name in 'host' connection string option."));
            Assert.That(exception.Message, Does.Contain("'notaport1' is not a valid Int32 value for the port in the 'host' connection string option."));
            Assert.That(exception.Message, Does.Contain("'notaport2' is not a valid Int32 value for the 'port' connection string option."));
            Assert.That(exception.Message, Does.Contain("'notusetls' is not a valid Boolean value for the 'useTls' connection string option."));
            Assert.That(exception.Message, Does.Contain("The 'UsePublisherConfirms' connection string option has been removed"));
            Assert.That(exception.Message, Does.Contain("The 'PrefetchCount' connection string option has been removed"));
            Assert.That(exception.Message, Does.Contain("The 'MaxWaitTimeForConfirms' connection string option has been removed"));
            Assert.That(exception.Message, Does.Contain("The 'DequeueTimeout' connection string option has been removed"));
            Assert.That(exception.Message, Does.Contain("The 'requestedHeartbeat' connection string option has been removed"));
            Assert.That(exception.Message, Does.Contain("The 'retryDelay' connection string option has been removed"));
            Assert.That(exception.Message, Does.Contain("The 'certPath' connection string option has been removed"));
            Assert.That(exception.Message, Does.Contain("The 'certPassphrase' connection string option has been removed"));
        }

        [Test]
        public void Should_set_default_port()
        {
            Assert.That(defaults.Port, Is.EqualTo(5672));
        }

        [Test]
        public void Should_set_default_virtual_host()
        {
            Assert.That(defaults.VirtualHost, Is.EqualTo("/"));
        }

        [Test]
        public void Should_set_default_username()
        {
            Assert.That(defaults.UserName, Is.EqualTo("guest"));
        }

        [Test]
        public void Should_set_default_password()
        {
            Assert.That(defaults.Password, Is.EqualTo("guest"));
        }

        [Test]
        public void Should_set_default_use_tls()
        {
            Assert.That(defaults.UseTls, Is.False);
        }
    }
}
