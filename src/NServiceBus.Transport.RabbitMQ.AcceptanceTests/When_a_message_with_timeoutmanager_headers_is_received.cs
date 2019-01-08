namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Extensibility;
    using Features;
    using global::RabbitMQ.Client;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using ObjectBuilder;
    using Routing;
    using Support;

    class When_a_message_with_timeout_manager_headers_is_received : NServiceBusAcceptanceTest
    {
        const string ForwardedMessageReceiver = "forwarded_message_receiver";

        [Test]
        public async Task Should_forward_message_using_ForwardCurrentMessageTo()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<HandlerThatForwards>(b => b.When((session, c) =>
                {
                    RawDispatchTestMessage(AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(HandlerThatForwards)));
                    return Task.FromResult(0);
                }))
                .WithEndpoint<ForwardReceiver>()
                .Done(c => c.GotForwardedMessage)
                .Run();

            Assert.AreEqual(1, context.ReceivedCount, "EndpointThatForwards received count mismatch");
            Assert.IsTrue(context.GotForwardedMessage);
        }

        [Test]
        public async Task Should_forward_message_when_messageforwarding_is_configured()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<SimpleEndpoint>(b => b.CustomConfig(c => c.ForwardReceivedMessagesTo(ForwardedMessageReceiver)).When((session, c) =>
                {
                    RawDispatchTestMessage(AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(SimpleEndpoint)));
                    return Task.FromResult(0);
                }))
                .WithEndpoint<ForwardReceiver>()
                .Done(c => c.GotForwardedMessage)
                .Run();

            Assert.AreEqual(1, context.ReceivedCount, "EndpointThatForwards received count mismatch");
            Assert.IsTrue(context.GotForwardedMessage);
        }

        [Test]
        public async Task Should_forward_message_when_auditing_is_configured()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<SimpleEndpoint>(b => b.CustomConfig(c => c.AuditProcessedMessagesTo(ForwardedMessageReceiver)).When((session, c) =>
                {
                    RawDispatchTestMessage(AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(SimpleEndpoint)));
                    return Task.FromResult(0);
                }))
                .WithEndpoint<ForwardReceiver>()
                .Done(c => c.GotForwardedMessage)
                .Run();

            Assert.AreEqual(1, context.ReceivedCount, "EndpointThatForwards received count mismatch");
            Assert.IsTrue(context.GotForwardedMessage);
        }

        [Test]
        public async Task Should_forward_message_when_using_idispatcher()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<SimpleEndpoint>(b => b.CustomConfig(c => c.AuditProcessedMessagesTo(ForwardedMessageReceiver)).When((session, c) =>
                {
                    var satelliteAddress = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(SimpleEndpoint)) + ".satellite";
                    RawDispatchTestMessage(satelliteAddress);
                    return Task.FromResult(0);
                }))
                .WithEndpoint<ForwardReceiver>()
                .Done(c => c.GotForwardedMessage)
                .Run();

            Assert.AreEqual(1, context.ReceivedCount, "EndpointThatForwards received count mismatch");
            Assert.IsTrue(context.GotForwardedMessage);
        }

        static class TimeoutManagerHeaders
        {
            public const string Expire = "NServiceBus.Timeout.Expire";
            public const string RouteExpiredTimeoutTo = "NServiceBus.Timeout.RouteExpiredTimeoutTo";
        }

        public class Context : ScenarioContext
        {
            public bool GotForwardedMessage { get; set; }

            public int ReceivedCount { get; set; }
        }

        public class ForwardReceiver : EndpointConfigurationBuilder
        {
            public ForwardReceiver()
            {
                EndpointSetup<DefaultServer>()
                    .CustomEndpointName(ForwardedMessageReceiver);
            }

            public class MessageToForwardHandler : IHandleMessages<MessageToForward>
            {
                public Context Context { get; set; }

                public Task Handle(MessageToForward message, IMessageHandlerContext context)
                {
                    Console.WriteLine("Handling message in ForwardReceiver");
                    Context.GotForwardedMessage = true;
                    return Task.FromResult(0);
                }
            }
        }

        public class HandlerThatForwards : EndpointConfigurationBuilder
        {
            public HandlerThatForwards()
            {
                EndpointSetup<DefaultServer>();
            }

            public class MessageToForwardHandler : IHandleMessages<MessageToForward>
            {
                public Context Context { get; set; }

                public Task Handle(MessageToForward message, IMessageHandlerContext context)
                {
                    Console.WriteLine("Handling message in HandlerThatForwards");
                    Context.ReceivedCount++;
                    return context.ForwardCurrentMessageTo(ForwardedMessageReceiver);
                }
            }
        }

        public class SimpleEndpoint : EndpointConfigurationBuilder
        {
            public SimpleEndpoint()
            {
                EndpointSetup<DefaultServer>();
            }

            public class MessageToForwardHandler : IHandleMessages<MessageToForward>
            {
                public Context Context { get; set; }

                public Task Handle(MessageToForward message, IMessageHandlerContext context)
                {
                    Console.WriteLine("Handling message in SimpleEndpoint");
                    Context.ReceivedCount++;
                    return Task.FromResult(0);
                }
            }

            public class SatelliteThatForwardsFeature :Feature
            {
                public SatelliteThatForwardsFeature()
                {
                    EnableByDefault();
                }

                protected override void Setup(FeatureConfigurationContext context)
                {
                    context.AddSatelliteReceiver(
                        name: "SatelliteThatForwards",
                        transportAddress: AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(SimpleEndpoint)) + ".satellite" ,
                        runtimeSettings: PushRuntimeSettings.Default,
                        recoverabilityPolicy: (config, errorContext) => { return RecoverabilityAction.MoveToError(config.Failed.ErrorQueue); },
                        onMessage: OnMessage);
                }

                Task OnMessage(IBuilder builder, MessageContext context)
                {
                    Console.WriteLine("Handling message in SatelliteThatForwards");
                    var dispatcher = builder.Build<IDispatchMessages>();

                    var testContext = builder.Build<Context>();
                    testContext.ReceivedCount++;

                    return dispatcher.Dispatch(new TransportOperations(new[] { new TransportOperation(new OutgoingMessage(context.MessageId, context.Headers, context.Body), new UnicastAddressTag(ForwardedMessageReceiver)),  }), new TransportTransaction(), new ContextBag());
                }
            }
        }

        public class MessageToForward : IMessage
        {
        }

        public void RawDispatchTestMessage(string queue)
        {
            var connectionConfiguration = ConnectionConfiguration.Create(Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString"), Guid.NewGuid().ToString());

            var connectionFactory = new ConnectionFactory
            {
                HostName = connectionConfiguration.Host,
                Port = connectionConfiguration.Port,
                VirtualHost = connectionConfiguration.VirtualHost,
                UserName = connectionConfiguration.UserName,
                Password = connectionConfiguration.Password,
                RequestedHeartbeat = connectionConfiguration.RequestedHeartbeat,
                NetworkRecoveryInterval = connectionConfiguration.RetryDelay,
                UseBackgroundThreadsForIO = true
            };

            connectionFactory.ClientProperties.Clear();

            foreach (var item in connectionConfiguration.ClientProperties)
            {
                connectionFactory.ClientProperties.Add(item.Key, item.Value);
            }

            using (var connection = connectionFactory.CreateConnection())
            {
                using (var channel = connection.CreateModel())
                {
                    var properties = channel.CreateBasicProperties();

                    var messageId = Guid.NewGuid().ToString();

                    properties.MessageId = messageId;

                    properties.Headers = new Dictionary<string, object>
                    {
                        {"NServiceBus.ContentType", "text/xml"},
                        {"NServiceBus.EnclosedMessageTypes", "NServiceBus.Transport.RabbitMQ.AcceptanceTests.When_a_message_with_timeout_manager_headers_is_received+MessageToForward, NServiceBus.Transport.RabbitMQ.AcceptanceTests, Version = 1.0.0.0, Culture = neutral, PublicKeyToken = null"},
                        {"NServiceBus.MessageId","213f55fc-caca-460c-bf6f-a9cf00044edf"},
                        {"NServiceBus.MessageIntent","Send"},
                        {TimeoutManagerHeaders.RouteExpiredTimeoutTo, queue},
                        {TimeoutManagerHeaders.Expire, DateTimeExtensions.ToWireFormattedString(DateTime.Now.AddSeconds(2))}
                };

                    var payload = "<MessageToForward></MessageToForward>";

                    channel.BasicPublish(
                        exchange: string.Empty,
                        routingKey: queue,
                        mandatory: false,
                        basicProperties: properties,
                        body: Encoding.UTF8.GetBytes(payload));
                }
            }
        }

        class ConnectionConfiguration
        {
            const bool defaultUseTls = false;
            const int defaultPort = 5672;
            const int defaultTlsPort = 5671;
            const string defaultVirtualHost = "/";
            const string defaultUserName = "guest";
            const string defaultPassword = "guest";
            const ushort defaultRequestedHeartbeat = 5;
            static readonly TimeSpan defaultRetryDelay = TimeSpan.FromSeconds(10);
            const string defaultCertPath = "";
            const string defaultCertPassphrase = null;

            public string Host { get; }

            public int Port { get; }

            public string VirtualHost { get; }

            public string UserName { get; }

            public string Password { get; }

            public ushort RequestedHeartbeat { get; }

            public TimeSpan RetryDelay { get; }

            public bool UseTls { get; }

            public string CertPath { get; }

            public string CertPassphrase { get; }

            public Dictionary<string, string> ClientProperties { get; }

            ConnectionConfiguration(
                string host,
                int port,
                string virtualHost,
                string userName,
                string password,
                ushort requestedHeartbeat,
                TimeSpan retryDelay,
                bool useTls,
                string certPath,
                string certPassphrase,
                Dictionary<string, string> clientProperties)
            {
                Host = host;
                Port = port;
                VirtualHost = virtualHost;
                UserName = userName;
                Password = password;
                RequestedHeartbeat = requestedHeartbeat;
                RetryDelay = retryDelay;
                UseTls = useTls;
                CertPath = certPath;
                CertPassphrase = certPassphrase;
                ClientProperties = clientProperties;
            }

            public static ConnectionConfiguration Create(string connectionString, string endpointName)
            {
                var dictionary = new DbConnectionStringBuilder { ConnectionString = connectionString }
                    .OfType<KeyValuePair<string, object>>()
                    .ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.OrdinalIgnoreCase);

                var invalidOptionsMessage = new StringBuilder();

                var useTls = GetValue(dictionary, "useTls", bool.TryParse, defaultUseTls, invalidOptionsMessage);
                var port = GetValue(dictionary, "port", int.TryParse, useTls ? defaultTlsPort : defaultPort, invalidOptionsMessage);
                var virtualHost = GetValue(dictionary, "virtualHost", defaultVirtualHost);
                var userName = GetValue(dictionary, "userName", defaultUserName);
                var password = GetValue(dictionary, "password", defaultPassword);
                var requestedHeartbeat = GetValue(dictionary, "requestedHeartbeat", ushort.TryParse, defaultRequestedHeartbeat, invalidOptionsMessage);
                var retryDelay = GetValue(dictionary, "retryDelay", TimeSpan.TryParse, defaultRetryDelay, invalidOptionsMessage);
                var certPath = GetValue(dictionary, "certPath", defaultCertPath);
                var certPassPhrase = GetValue(dictionary, "certPassphrase", defaultCertPassphrase);

                var value = dictionary["host"];
                var hostsAndPorts = value.Split(',');

                var parts = hostsAndPorts[0].Split(':');
                var host = parts.ElementAt(0);

                var nsbVersion = FileVersionInfo.GetVersionInfo(typeof(Endpoint).Assembly.Location);
                var nsbFileVersion = $"{nsbVersion.FileMajorPart}.{nsbVersion.FileMinorPart}.{nsbVersion.FileBuildPart}";

                var rabbitMQVersion = FileVersionInfo.GetVersionInfo(typeof(ConnectionConfiguration).Assembly.Location);
                var rabbitMQFileVersion = $"{rabbitMQVersion.FileMajorPart}.{rabbitMQVersion.FileMinorPart}.{rabbitMQVersion.FileBuildPart}";

                var applicationNameAndPath = Environment.GetCommandLineArgs()[0];
                var applicationName = Path.GetFileName(applicationNameAndPath);
                var applicationPath = Path.GetDirectoryName(applicationNameAndPath);

                var hostname = RuntimeEnvironment.MachineName;

                var clientProperties = new Dictionary<string, string>
                {
                    { "client_api", "NServiceBus" },
                    { "nservicebus_version", nsbFileVersion },
                    { "nservicebus.rabbitmq_version", rabbitMQFileVersion },
                    { "application", applicationName },
                    { "application_location", applicationPath },
                    { "machine_name", hostname },
                    { "user", userName },
                    { "endpoint_name", endpointName },
                };

                return new ConnectionConfiguration(
                    host, port, virtualHost, userName, password, requestedHeartbeat, retryDelay, useTls, certPath, certPassPhrase, clientProperties);
            }

            static string GetValue(Dictionary<string, string> dictionary, string key, string defaultValue)
            {
                return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
            }

            static T GetValue<T>(Dictionary<string, string> dictionary, string key, Convert<T> convert, T defaultValue, StringBuilder invalidOptionsMessage)
            {
                if (dictionary.TryGetValue(key, out var value))
                {
                    if (!convert(value, out defaultValue))
                    {
                        invalidOptionsMessage.AppendLine($"'{value}' is not a valid {typeof(T).Name} value for the '{key}' connection string option.");
                    }
                }

                return defaultValue;
            }

            delegate bool Convert<T>(string input, out T output);
        }
    }
}
