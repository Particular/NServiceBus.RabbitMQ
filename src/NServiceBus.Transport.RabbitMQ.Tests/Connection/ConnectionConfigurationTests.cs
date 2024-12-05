namespace NServiceBus.Transport.RabbitMQ.Tests.ConnectionString
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client.Exceptions;
    using NUnit.Framework;
    using RabbitMQ;

    [TestFixture]
    class ConnectionConfigurationTests
    {
        const string connectionString = "virtualHost=Copa;username=Copa;host=192.168.1.1:1234;password=abc_xyz;port=12345;useTls=true";
        protected string ReceiverQueue => GetTestQueueName("testreceiver");
        protected string ErrorQueue => GetTestQueueName("error");
        protected string GetTestQueueName(string queueName) => $"{queueName}-{queueType}";
        protected IList<string> AdditionalReceiverQueues = [];
        protected string[] SendingAddresses => [.. AdditionalReceiverQueues, ErrorQueue];
        protected QueueType queueType = QueueType.Quorum;

        protected HostSettings HostSettings => new(ReceiverQueue, ReceiverQueue, new StartupDiagnosticEntries(), (_, _, _) => { }, true);
        protected ReceiveSettings[] ReceiveSettings =>
        [
           new ReceiveSettings( ReceiverQueue, new QueueAddress(ReceiverQueue), true, true, ErrorQueue)
        ];

        readonly ConnectionConfiguration brokerDefaults = ConnectionConfiguration.Create("host=localhost");
        readonly ConnectionConfiguration managementDefaults = ConnectionConfiguration.Create("host=localhost", isManagementConnection: true);


        [Test]
        public void Should_correctly_parse_full_connection_string()
        {
            var connectionConfiguration = ConnectionConfiguration.Create(connectionString);

            Assert.Multiple(() =>
            {
                Assert.That(connectionConfiguration.Host, Is.EqualTo("192.168.1.1"));
                Assert.That(connectionConfiguration.Port, Is.EqualTo(1234));
                Assert.That(connectionConfiguration.VirtualHost, Is.EqualTo("Copa"));
                Assert.That(connectionConfiguration.UserName, Is.EqualTo("Copa"));
                Assert.That(connectionConfiguration.Password, Is.EqualTo("abc_xyz"));
                Assert.That(connectionConfiguration.UseTls, Is.EqualTo(true));
            });
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

            Assert.Multiple(() =>
            {
                Assert.That(connectionConfiguration.Host, Is.EqualTo("host.one"));
                Assert.That(connectionConfiguration.Port, Is.EqualTo(1001));
            });
        }

        [Test]
        public void Should_parse_host_with_separate_port()
        {
            var connectionConfiguration = ConnectionConfiguration.Create("host=my.host.com;port=1234");

            Assert.Multiple(() =>
            {
                Assert.That(connectionConfiguration.Host, Is.EqualTo("my.host.com"));
                Assert.That(connectionConfiguration.Port, Is.EqualTo(1234));
            });
        }

        [Test]
        public void Should_parse_host_without_port()
        {
            var connectionConfiguration = ConnectionConfiguration.Create("host=my.host.com");

            Assert.Multiple(() =>
            {
                Assert.That(connectionConfiguration.Host, Is.EqualTo("my.host.com"));
                Assert.That(connectionConfiguration.Port, Is.EqualTo(5672));
            });
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

            Assert.Multiple(() =>
            {
                Assert.That(connectionConfiguration.UseTls, Is.EqualTo(true));
                Assert.That(connectionConfiguration.Port, Is.EqualTo(5671));
            });
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
            Assert.Multiple(() =>
            {
                Assert.That(brokerDefaults.Port, Is.EqualTo(5672));
                Assert.That(managementDefaults.Port, Is.EqualTo(15672));
            });
        }

        [Test]
        public void Should_set_default_virtual_host()
        {
            Assert.Multiple(() =>
            {
                Assert.That(brokerDefaults.VirtualHost, Is.EqualTo("/"));
                Assert.That(managementDefaults.VirtualHost, Is.EqualTo("/"));
            });
        }

        [Test]
        public void Should_set_default_username()
        {
            Assert.Multiple(() =>
            {
                Assert.That(brokerDefaults.UserName, Is.EqualTo("guest"));
                Assert.That(managementDefaults.UserName, Is.EqualTo("guest"));
            });
        }

        [Test]
        public void Should_set_default_password()
        {
            Assert.Multiple(() =>
            {
                Assert.That(brokerDefaults.Password, Is.EqualTo("guest"));
                Assert.That(managementDefaults.Password, Is.EqualTo("guest"));
            });
        }

        [Test]
        public void Should_set_default_use_tls()
        {
            Assert.Multiple(() =>
            {
                Assert.That(brokerDefaults.UseTls, Is.EqualTo(false));
                Assert.That(managementDefaults.UseTls, Is.EqualTo(false));
            });
        }

        [Test]
        public void Should_configure_broker_and_management_connection_configurations_with_single_connection_string()
        {
            var transport = new RabbitMQTransport(RoutingTopology.Conventional(QueueType.Quorum), "virtualHost=/;host=localhost;username=guest;password=guest;port=5672;useTls=false");

            Assert.Multiple(() =>
            {
                Assert.That(transport.ConnectionConfiguration.VirtualHost, Is.EqualTo("/"));
                Assert.That(transport.ConnectionConfiguration.Host, Is.EqualTo("localhost"));
                Assert.That(transport.ConnectionConfiguration.UserName, Is.EqualTo("guest"));
                Assert.That(transport.ConnectionConfiguration.Password, Is.EqualTo("guest"));
                Assert.That(transport.ConnectionConfiguration.Port, Is.EqualTo(5672));
                Assert.That(transport.ConnectionConfiguration.UseTls, Is.EqualTo(false));

                Assert.That(transport.ManagementConnectionConfiguration.VirtualHost, Is.EqualTo("/"));
                Assert.That(transport.ManagementConnectionConfiguration.Host, Is.EqualTo("localhost"));
                Assert.That(transport.ManagementConnectionConfiguration.UserName, Is.EqualTo("guest"));
                Assert.That(transport.ManagementConnectionConfiguration.Password, Is.EqualTo("guest"));
                Assert.That(transport.ManagementConnectionConfiguration.Port, Is.EqualTo(15672));  // This should be set to the default management port
                Assert.That(transport.ManagementConnectionConfiguration.UseTls, Is.EqualTo(false));
            });
        }

        [Test]
        public void Should_configure_broker_and_management_connection_configurations_with_respective_connection_strings()
        {
            var transport = new RabbitMQTransport(RoutingTopology.Conventional(QueueType.Quorum), "virtualHost=/;host=localhost;username=guest;password=guest;port=5672;useTls=false", connectionString);

            Assert.Multiple(() =>
            {
                Assert.That(transport.ConnectionConfiguration.VirtualHost, Is.EqualTo("/"));
                Assert.That(transport.ConnectionConfiguration.Host, Is.EqualTo("localhost"));
                Assert.That(transport.ConnectionConfiguration.UserName, Is.EqualTo("guest"));
                Assert.That(transport.ConnectionConfiguration.Password, Is.EqualTo("guest"));
                Assert.That(transport.ConnectionConfiguration.Port, Is.EqualTo(5672));
                Assert.That(transport.ConnectionConfiguration.UseTls, Is.EqualTo(false));

                Assert.That(transport.ManagementConnectionConfiguration.VirtualHost, Is.EqualTo("Copa"));
                Assert.That(transport.ManagementConnectionConfiguration.Host, Is.EqualTo("192.168.1.1"));
                Assert.That(transport.ManagementConnectionConfiguration.UserName, Is.EqualTo("Copa"));
                Assert.That(transport.ManagementConnectionConfiguration.Password, Is.EqualTo("abc_xyz"));
                Assert.That(transport.ManagementConnectionConfiguration.Port, Is.EqualTo(1234));
                Assert.That(transport.ManagementConnectionConfiguration.UseTls, Is.EqualTo(true));
            });
        }

        [Test]
        public void Should_throw_on_invalid_management_connection_string()
        {
            var transport = new RabbitMQTransport(RoutingTopology.Conventional(QueueType.Quorum), "virtualHost=/;username=guest;host=localhost;password=guest;port=5672;useTls=false", "host=127.0.0.1;username=Copa");

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await transport.Initialize(HostSettings, ReceiveSettings, SendingAddresses).ConfigureAwait(false));

            Assert.That(exception.Message, Does.Contain("Could not access RabbitMQ Management API"));
        }

        [Test]
        public void Should_throw_on_invalid_broker_connection_string()
        {
            var transport = new RabbitMQTransport(RoutingTopology.Conventional(QueueType.Quorum), "host=127.0.0.1;username=Copa", "virtualHost=/;username=guest;host=localhost;password=guest;port=15672;useTls=false");

            var exception = Assert.ThrowsAsync<BrokerUnreachableException>(async () => await transport.Initialize(HostSettings, ReceiveSettings, SendingAddresses).ConfigureAwait(false));

            Assert.That(exception.Message, Does.Contain("None of the specified endpoints were reachable"));
        }

        [Test]
        public void Should_throw_on_invalid_legacy_management_connection_string()
        {
            // Create transport in legacy mode
            var transport = new RabbitMQTransport
            {
                TopologyFactory = durable => new ConventionalRoutingTopology(durable, queueType),
                LegacyApiConnectionString = "virtualHost=/;username=guest;host=localhost;password=guest;port=5672;useTls=false",
                LegacyManagementApiConnectionString = "host=127.0.0.1;username=Copa"
            };

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await transport.Initialize(HostSettings, ReceiveSettings, SendingAddresses).ConfigureAwait(false));

            Assert.That(exception.Message, Does.Contain("Could not access RabbitMQ Management API"));
        }

        [Test]
        public void Should_throw_on_invalid_legacy_broker_connection_string()
        {
            // Create transport in legacy mode
            var transport = new RabbitMQTransport
            {
                TopologyFactory = durable => new ConventionalRoutingTopology(durable, queueType),
                LegacyApiConnectionString = "virtualHost=/;username=Copa;host=localhost;password=guest;port=5672;useTls=false",
                LegacyManagementApiConnectionString = "host=127.0.0.1;username=guest"
            };

            var exception = Assert.ThrowsAsync<BrokerUnreachableException>(async () => await transport.Initialize(HostSettings, ReceiveSettings, SendingAddresses).ConfigureAwait(false));

            Assert.That(exception.Message, Does.Contain("None of the specified endpoints were reachable"));

        }

        [Test]
        public async Task Should_connect_to_management_api_with_broker_credentials()
        {
            var transport = new RabbitMQTransport(RoutingTopology.Conventional(QueueType.Quorum), "virtualHost=/;username=guest;host=localhost;password=guest;port=5672;useTls=false");

            var infra = await transport.Initialize(HostSettings, ReceiveSettings, SendingAddresses).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(transport.ConnectionConfiguration.Port, Is.EqualTo(5672));
                Assert.That(transport.ManagementConnectionConfiguration.Port, Is.EqualTo(15672));
            });

        }

        [Test]
        public async Task Should_set_default_port_values_for_broker_and_management_connections()
        {
            var transport = new RabbitMQTransport(RoutingTopology.Conventional(QueueType.Quorum), "host=localhost");

            _ = await transport.Initialize(HostSettings, ReceiveSettings, SendingAddresses).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(transport.ConnectionConfiguration.Port, Is.EqualTo(5672));
                Assert.That(transport.ManagementConnectionConfiguration.Port, Is.EqualTo(15672));
            });

        }

        [Test]
        public async Task Should_not_throw_when_DoNotUseManagementClient_is_enabled_and_management_connection_is_invalid()
        {
            var transport = new RabbitMQTransport(RoutingTopology.Conventional(QueueType.Quorum), "host=localhost", "host=Copa")
            {
                DoNotUseManagementClient = true
            };

            _ = await transport.Initialize(HostSettings, ReceiveSettings, SendingAddresses).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(transport.ConnectionConfiguration.Port, Is.EqualTo(5672));
                Assert.That(transport.ManagementConnectionConfiguration.Port, Is.EqualTo(15672));
            });

        }
    }
}
