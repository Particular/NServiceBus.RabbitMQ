namespace NServiceBus.Transport.RabbitMQ.Tests.ConnectionString
{
    using System;
    using NUnit.Framework;
    using RabbitMQ;

    [TestFixture]
    public class ConnectionConfigurationTests
    {
        const string connectionString =
            "virtualHost=Copa;username=Copa;host=192.168.1.1:1234;password=abc_xyz;port=12345;requestedHeartbeat=3;" +
            "retryDelay=01:02:03;useTls=true;certPath=/path/to/client/keycert.p12;certPassPhrase=abc123";

        const string endpointName = "endpoint";

        ConnectionConfiguration defaults = ConnectionConfiguration.Create("host=localhost", "endpoint");

        [Test]
        public void Should_correctly_parse_full_connection_string()
        {
            var connectionConfiguration = ConnectionConfiguration.Create(connectionString, endpointName);

            Assert.AreEqual(connectionConfiguration.Host, "192.168.1.1");
            Assert.AreEqual(connectionConfiguration.Port, 1234);
            Assert.AreEqual(connectionConfiguration.VirtualHost, "Copa");
            Assert.AreEqual(connectionConfiguration.UserName, "Copa");
            Assert.AreEqual(connectionConfiguration.Password, "abc_xyz");
            Assert.AreEqual(connectionConfiguration.RequestedHeartbeat, 3);
            Assert.AreEqual(connectionConfiguration.RetryDelay, new TimeSpan(1, 2, 3)); //01:02:03
            Assert.AreEqual(connectionConfiguration.UseTls, true);
            Assert.AreEqual(connectionConfiguration.CertPath, "/path/to/client/keycert.p12");
            Assert.AreEqual(connectionConfiguration.CertPassphrase, "abc123");
        }

        [Test]
        public void Should_fail_if_host_is_not_present()
        {
            Assert.Throws<NotSupportedException>(() => ConnectionConfiguration.Create("virtualHost=Copa;username=Copa;password=abc_xyz;port=12345;requestedHeartbeat=3", endpointName));
        }

        [Test]
        public void Should_parse_host()
        {
            var connectionConfiguration = ConnectionConfiguration.Create("host=host.one:1001;port=1002", endpointName);

            Assert.AreEqual(connectionConfiguration.Host, "host.one");
            Assert.AreEqual(connectionConfiguration.Port, 1001);
        }

        [Test]
        public void Should_parse_host_with_separate_port()
        {
            var connectionConfiguration = ConnectionConfiguration.Create("host=my.host.com;port=1234", endpointName);

            Assert.AreEqual(connectionConfiguration.Host, "my.host.com");
            Assert.AreEqual(connectionConfiguration.Port, 1234);
        }

        [Test]
        public void Should_parse_host_without_port()
        {
            var connectionConfiguration = ConnectionConfiguration.Create("host=my.host.com", endpointName);

            Assert.AreEqual(connectionConfiguration.Host, "my.host.com");
            Assert.AreEqual(connectionConfiguration.Port, 5672);
        }

        [Test]
        public void Should_parse_the_hostname()
        {
            var connectionConfiguration = ConnectionConfiguration.Create("host=myHost", endpointName);

            Assert.AreEqual("myHost", connectionConfiguration.Host);
        }

        [Test]
        public void Should_parse_the_password()
        {
            var connectionConfiguration = ConnectionConfiguration.Create("host=localhost;password=test", endpointName);

            Assert.AreEqual("test", connectionConfiguration.Password);
        }

        [Test]
        public void Should_parse_the_port()
        {
            var connectionConfiguration = ConnectionConfiguration.Create("host=localhost;port=8181", endpointName);

            Assert.AreEqual(8181, connectionConfiguration.Port);
        }

        [Test]
        public void Should_parse_the_requestedHeartbeat()
        {
            var connectionConfiguration = ConnectionConfiguration.Create("host=localhost;requestedHeartbeat=5", endpointName);

            Assert.AreEqual(5, connectionConfiguration.RequestedHeartbeat);
        }

        [Test]
        public void Should_parse_the_retry_delay()
        {
            var connectionConfiguration = ConnectionConfiguration.Create("host=localhost;retryDelay=00:00:10", endpointName);

            Assert.AreEqual(TimeSpan.FromSeconds(10), connectionConfiguration.RetryDelay);
        }

        [Test]
        public void Should_parse_the_username()
        {
            var connectionConfiguration = ConnectionConfiguration.Create("host=localhost;username=test", endpointName);

            Assert.AreEqual("test", connectionConfiguration.UserName);
        }

        [Test]
        public void Should_parse_the_virtual_hostname()
        {
            var connectionConfiguration = ConnectionConfiguration.Create("host=localhost;virtualHost=myVirtualHost", endpointName);

            Assert.AreEqual("myVirtualHost", connectionConfiguration.VirtualHost);
        }

        [Test]
        public void Should_parse_use_tls()
        {
            var connectionConfiguration = ConnectionConfiguration.Create("host=localhost;useTls=true", endpointName);

            Assert.AreEqual(true, connectionConfiguration.UseTls);
            Assert.AreEqual(5671, connectionConfiguration.Port);
        }

        [Test]
        public void Should_parse_the_cert_path()
        {
            var connectionConfiguration = ConnectionConfiguration.Create("host=localhost;certPath=/path/keyfile.p12", endpointName);

            Assert.AreEqual("/path/keyfile.p12", connectionConfiguration.CertPath);
        }

        [Test]
        public void Should_parse_the_cert_passphrase()
        {
            var connectionConfiguration = ConnectionConfiguration.Create("host=localhost;certPassphrase=abc123", endpointName);

            Assert.AreEqual("abc123", connectionConfiguration.CertPassphrase);
        }

        [Test]
        public void Should_throw_if_given_badly_formatted_retry_delay()
        {
            var formatException = Assert.Throws<FormatException>(
                () => ConnectionConfiguration.Create("host=localhost;retryDelay=00:0d0:10", endpointName));

            Assert.AreEqual("00:0d0:10 is not a valid value for TimeSpan.", formatException.Message);
        }

        [Test]
        public void Should_throw_on_malformed_string()
        {
            Assert.Throws<ArgumentException>(() => ConnectionConfiguration.Create("not a well formed name value pair;", endpointName));
        }

        [Test]
        public void Should_inform_that_multiple_hosts_are_not_supported()
        {
            var exception = Assert.Throws<NotSupportedException>(() => ConnectionConfiguration.Create("host=localhost,host=localhost2", endpointName));

            Assert.That(exception.Message, Does.Contain("Multiple hosts are no longer supported"));
            Assert.That(exception.Message, Does.Contain("consider using a load balancer"));
        }

        [Test]
        public void Should_inform_that_dequeuetimeout_has_been_removed()
        {
            var exception = Assert.Throws<NotSupportedException>(() => ConnectionConfiguration.Create("host=localhost;dequeuetimeout=1", endpointName));

            Assert.That(exception.Message, Does.Contain("The 'DequeueTimeout' connection string option has been removed"));
        }

        [Test]
        public void Should_inform_that_max_wait_time_for_confirmss_has_been_removed()
        {
            var exception = Assert.Throws<NotSupportedException>(() => ConnectionConfiguration.Create("host=localhost;maxWaitTimeForConfirms=02:03:39", endpointName));

            Assert.That(exception.Message, Does.Contain("The 'MaxWaitTimeForConfirms' connection string option has been removed"));
        }

        [Test]
        public void Should_inform_that_prefetchcount_has_been_removed()
        {
            var exception = Assert.Throws<NotSupportedException>(() => ConnectionConfiguration.Create("host=localhost;prefetchcount=100", endpointName));

            Assert.That(exception.Message, Does.Contain("The 'PrefetchCount' connection string option has been removed"));
        }

        [Test]
        public void Should_inform_that_usepublisherconfirms_has_been_removed()
        {
            var exception = Assert.Throws<NotSupportedException>(() => ConnectionConfiguration.Create("host=localhost;usePublisherConfirms=true", endpointName));

            Assert.That(exception.Message, Does.Contain("The 'UsePublisherConfirms' connection string option has been removed"));
        }

        [Test]
        public void Should_list_all_invalid_options()
        {
            var exception = Assert.Throws<NotSupportedException>(() => ConnectionConfiguration.Create("host=localhost,host=localhost2;usePublisherConfirms=true;prefetchcount=100;maxWaitTimeForConfirms=02:03:39;dequeuetimeout=1", endpointName));

            Assert.That(exception.Message, Does.Contain("Multiple hosts are no longer supported"));
            Assert.That(exception.Message, Does.Contain("consider using a load balancer"));
            Assert.That(exception.Message, Does.Contain("The 'UsePublisherConfirms' connection string option has been removed"));
            Assert.That(exception.Message, Does.Contain("The 'PrefetchCount' connection string option has been removed"));
            Assert.That(exception.Message, Does.Contain("The 'MaxWaitTimeForConfirms' connection string option has been removed"));
            Assert.That(exception.Message, Does.Contain("The 'DequeueTimeout' connection string option has been removed"));
        }

        [Test]
        public void Should_set_default_port()
        {
            Assert.AreEqual(defaults.Port, 5672);
        }

        [Test]
        public void Should_set_default_virtual_host()
        {
            Assert.AreEqual(defaults.VirtualHost, "/");
        }

        [Test]
        public void Should_set_default_username()
        {
            Assert.AreEqual(defaults.UserName, "guest");
        }

        [Test]
        public void Should_set_default_password()
        {
            Assert.AreEqual(defaults.Password, "guest");
        }

        [Test]
        public void Should_set_default_requested_heartbeat()
        {
            Assert.AreEqual(defaults.RequestedHeartbeat, 5);
        }

        [Test]
        public void Should_set_default_retry_delay()
        {
            Assert.AreEqual(defaults.RetryDelay, TimeSpan.FromSeconds(10));
        }

        [Test]
        public void Should_set_default_use_tls()
        {
            Assert.AreEqual(defaults.UseTls, false);
        }

        [Test]
        public void Should_set_default_cert_path()
        {
            Assert.AreEqual(defaults.CertPath, "");
        }

        [Test]
        public void Should_set_default_retry_cert_passphrase()
        {
            Assert.AreEqual(defaults.CertPassphrase, null);
        }
    }
}
