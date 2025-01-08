#nullable enable

namespace NServiceBus.Transport.RabbitMQ.Tests.ConnectionString
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client.Exceptions;
    using NUnit.Framework;
    using RabbitMQ;

    [TestFixture]
    class ConnectionConfigurationTests
    {
        const string FakeConnectionString = "virtualHost=Copa;username=Copa;host=192.168.1.1:1234;password=abc_xyz;port=12345;useTls=true";
        static string BrokerConnectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString") ?? "host=localhost";
        static string ManagementConnectionString => CreateManagementConnectionString(BrokerConnectionString);

        static HostSettings HostSettings { get; } = new(nameof(ConnectionConfigurationTests), nameof(ConnectionConfigurationTests), null, null, false);

        static readonly ConnectionConfiguration brokerDefaults = ConnectionConfiguration.Create("host=localhost");
        static readonly ConnectionConfiguration managementDefaults = ConnectionConfiguration.Create("host=localhost");

        static string CreateManagementConnectionString(string connectionString)
        {
            var parameters = connectionString.Split(';').Select(param => param.Split('=')).ToDictionary(parts => parts[0], parts => parts[1]);
            parameters["port"] = "15672";
            return string.Join(";", parameters.Select(kv => $"{kv.Key}={kv.Value}"));
        }

        [Test]
        public void Should_correctly_parse_full_connection_string()
        {
            var connectionConfiguration = ConnectionConfiguration.Create(FakeConnectionString);

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

            var exception = Assert.Throws<NotSupportedException>(() => ConnectionConfiguration.Create(connectionString))
                ?? throw new ArgumentNullException("exception");

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
                Assert.That(managementDefaults.Port, Is.EqualTo(5672));
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
                Assert.That(transport.BrokerConnectionConfiguration.VirtualHost, Is.EqualTo("/"));
                Assert.That(transport.BrokerConnectionConfiguration.Host, Is.EqualTo("localhost"));
                Assert.That(transport.BrokerConnectionConfiguration.UserName, Is.EqualTo("guest"));
                Assert.That(transport.BrokerConnectionConfiguration.Password, Is.EqualTo("guest"));
                Assert.That(transport.BrokerConnectionConfiguration.Port, Is.EqualTo(5672));
                Assert.That(transport.BrokerConnectionConfiguration.UseTls, Is.EqualTo(false));

                Assert.That(transport.ManagementConnectionConfiguration.VirtualHost, Is.EqualTo("/"));
                Assert.That(transport.ManagementConnectionConfiguration.Host, Is.EqualTo("localhost"));
                Assert.That(transport.ManagementConnectionConfiguration.UserName, Is.EqualTo("guest"));
                Assert.That(transport.ManagementConnectionConfiguration.Password, Is.EqualTo("guest"));
                Assert.That(transport.ManagementConnectionConfiguration.Port, Is.EqualTo(5672));  // This should be set to the default management port
                Assert.That(transport.ManagementConnectionConfiguration.UseTls, Is.EqualTo(false));
            });
        }

        [Test]
        public void Should_configure_broker_and_management_connection_configurations_with_respective_connection_strings()
        {
            var transport = new RabbitMQTransport(RoutingTopology.Conventional(QueueType.Quorum), "virtualHost=/;host=localhost;username=guest;password=guest;port=5672;useTls=false", FakeConnectionString);

            Assert.Multiple(() =>
            {
                Assert.That(transport.BrokerConnectionConfiguration.VirtualHost, Is.EqualTo("/"));
                Assert.That(transport.BrokerConnectionConfiguration.Host, Is.EqualTo("localhost"));
                Assert.That(transport.BrokerConnectionConfiguration.UserName, Is.EqualTo("guest"));
                Assert.That(transport.BrokerConnectionConfiguration.Password, Is.EqualTo("guest"));
                Assert.That(transport.BrokerConnectionConfiguration.Port, Is.EqualTo(5672));
                Assert.That(transport.BrokerConnectionConfiguration.UseTls, Is.EqualTo(false));

                Assert.That(transport.ManagementConnectionConfiguration.VirtualHost, Is.EqualTo("Copa"));
                Assert.That(transport.ManagementConnectionConfiguration.Host, Is.EqualTo("192.168.1.1"));
                Assert.That(transport.ManagementConnectionConfiguration.UserName, Is.EqualTo("Copa"));
                Assert.That(transport.ManagementConnectionConfiguration.Password, Is.EqualTo("abc_xyz"));
                Assert.That(transport.ManagementConnectionConfiguration.Port, Is.EqualTo(1234));
                Assert.That(transport.ManagementConnectionConfiguration.UseTls, Is.EqualTo(true));
            });
        }

        [Test]
        public void Should_throw_on_invalid_management_credentials()
        {
            var invalidManagementConnection = new FakeConnectionConfiguration(ManagementConnectionString)
            {
                UserName = "Copa"
            };

            var transport = new RabbitMQTransport(RoutingTopology.Conventional(QueueType.Quorum), BrokerConnectionString, invalidManagementConnection.ToConnectionString());

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await transport.Initialize(HostSettings, [], []))
                ?? throw new ArgumentNullException("exception");

            Assert.That(exception.Message, Does.Contain("Could not access RabbitMQ Management API"));
        }

        [Test]
        public void Should_throw_on_invalid_management_host()
        {
            var invalidManagementConnection = new FakeConnectionConfiguration(ManagementConnectionString)
            {
                Host = "WrongHostName"
            };

            var transport = new RabbitMQTransport(RoutingTopology.Conventional(QueueType.Quorum), BrokerConnectionString, invalidManagementConnection.ToConnectionString());

            _ = Assert.ThrowsAsync<HttpRequestException>(async () => await transport.Initialize(HostSettings, [], []));
        }

        [Test]
        public void Should_throw_on_invalid_broker_connection_string()
        {
            var invalidBrokerConnection = new FakeConnectionConfiguration(host: "127.0.0.1", userName: "Copa");

            var transport = new RabbitMQTransport(RoutingTopology.Conventional(QueueType.Quorum), invalidBrokerConnection.ToConnectionString(), ManagementConnectionString);

            var exception = Assert.ThrowsAsync<BrokerUnreachableException>(async () => await transport.Initialize(HostSettings, [], []))
                ?? throw new ArgumentNullException("exception");

            Assert.That(exception.Message, Does.Contain("None of the specified endpoints were reachable"));
        }

        [Test]
        public void Should_throw_on_invalid_legacy_management_credentials()
        {
            var invalidManagementConnection = new FakeConnectionConfiguration(ManagementConnectionString)
            {
                UserName = "Copa"
            };

            // Create transport in legacy mode
            var transport = new RabbitMQTransport
            {
                TopologyFactory = durable => new ConventionalRoutingTopology(durable, QueueType.Quorum),
                LegacyApiConnectionString = BrokerConnectionString,
                LegacyManagementApiConnectionString = invalidManagementConnection.ToConnectionString()
            };

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await transport.Initialize(HostSettings, [], []));

            Assert.That(exception!.Message, Does.Contain("Could not access RabbitMQ Management API"));
        }

        [Test]
        public void Should_throw_on_invalid_legacy_management_host()
        {
            var invalidManagementConnection = new FakeConnectionConfiguration(ManagementConnectionString)
            {
                Host = "WrongHostName"
            };

            // Create transport in legacy mode
            var transport = new RabbitMQTransport
            {
                TopologyFactory = durable => new ConventionalRoutingTopology(durable, QueueType.Quorum),
                LegacyApiConnectionString = BrokerConnectionString,
                LegacyManagementApiConnectionString = invalidManagementConnection.ToConnectionString()
            };

            _ = Assert.ThrowsAsync<HttpRequestException>(async () => await transport.Initialize(HostSettings, [], []));
        }

        [Test]
        public void Should_throw_on_invalid_legacy_broker_connection_string()
        {
            var invalidBrokerConnection = new FakeConnectionConfiguration(host: "localhost", port: "5672", virtualHost: "/", userName: "Copa", password: "guest", useTls: "false");

            // Create transport in legacy mode
            var transport = new RabbitMQTransport
            {
                TopologyFactory = durable => new ConventionalRoutingTopology(durable, QueueType.Quorum),
                LegacyApiConnectionString = invalidBrokerConnection.ToConnectionString(),
                LegacyManagementApiConnectionString = ManagementConnectionString
            };

            var exception = Assert.ThrowsAsync<BrokerUnreachableException>(async () => await transport.Initialize(HostSettings, [], []))
                ?? throw new ArgumentNullException("exception");

            Assert.That(exception.Message, Does.Contain("None of the specified endpoints were reachable"));
        }

        [Test]
        public void Should_connect_to_management_api_with_broker_credentials()
        {
            var transport = new RabbitMQTransport(RoutingTopology.Conventional(QueueType.Quorum), BrokerConnectionString);

            Assert.DoesNotThrowAsync(async () => await transport.Initialize(HostSettings, [], []));
        }

        [Test]
        public async Task Should_set_default_port_values_for_broker_and_management_connections()
        {
            var validConnectionWithoutPort = new FakeConnectionConfiguration(BrokerConnectionString)
            {
                Port = null
            };

            var transport = new RabbitMQTransport(RoutingTopology.Conventional(QueueType.Quorum), validConnectionWithoutPort.ToConnectionString());

            _ = await transport.Initialize(HostSettings, [], []);

            Assert.Multiple(() =>
            {
                Assert.That(transport.BrokerConnectionConfiguration.Port, Is.EqualTo(5672));
                Assert.That(transport.ManagementConnectionConfiguration.Port, Is.EqualTo(5672));
            });
        }

        [Test]
        public void Should_not_throw_when_DoNotUseManagementClient_is_enabled_and_management_connection_is_invalid()
        {
            var invalidManagementConnection = new FakeConnectionConfiguration(host: "Copa");

            var transport = new RabbitMQTransport(RoutingTopology.Conventional(QueueType.Quorum), BrokerConnectionString, invalidManagementConnection.ToConnectionString())
            {
                DoNotUseManagementClient = true
            };

            Assert.DoesNotThrowAsync(async () => await transport.Initialize(HostSettings, [], []));
        }

        public class FakeConnectionConfiguration
        {
            internal string Host { get; set; }

            internal string? Port { get; set; }

            internal string? VirtualHost { get; set; }

            internal string? UserName { get; set; }

            internal string? Password { get; set; }

            internal string? UseTls { get; set; }

            internal FakeConnectionConfiguration(
                string host,
                string? port = null,
                string? virtualHost = null,
                string? userName = null,
                string? password = null,
                string? useTls = null)
            {
                Host = host;
                Port = port;
                VirtualHost = virtualHost;
                UserName = userName;
                Password = password;
                UseTls = useTls;
            }

            internal FakeConnectionConfiguration(string connectionString)
            {
                var parameters = connectionString.Split(';').Select(param => param.Split('=')).ToDictionary(parts => parts[0].ToLower(), parts => parts[1]);

                Host = parameters["host"];
                Port = GetParameterValue(parameters, "port");
                VirtualHost = GetParameterValue(parameters, "virtualhost");
                UserName = GetParameterValue(parameters, "username");
                Password = GetParameterValue(parameters, "password");
                UseTls = GetParameterValue(parameters, "usetls");
            }

            static string? GetParameterValue(Dictionary<string, string> parameters, string key) => parameters.TryGetValue(key, out var value) ? value : null;

            internal string ToConnectionString()
            {
                var sb = new StringBuilder();
                _ = sb.Append($"{nameof(Host)}={Host}");

                if (!string.IsNullOrEmpty(VirtualHost))
                {
                    _ = sb.Append($";{nameof(VirtualHost)}={VirtualHost}");
                }
                if (!string.IsNullOrEmpty(Port))
                {
                    _ = sb.Append($";{nameof(Port)}={Port}");
                }
                if (!string.IsNullOrEmpty(UserName))
                {
                    _ = sb.Append($";{nameof(UserName)}={UserName}");
                }
                if (!string.IsNullOrEmpty(Password))
                {
                    _ = sb.Append($";{nameof(Password)}={Password}");
                }
                if (!string.IsNullOrEmpty(UseTls))
                {
                    _ = sb.Append($";{nameof(UseTls)}={UseTls}");
                }
                return sb.ToString();
            }
        }
    }
}