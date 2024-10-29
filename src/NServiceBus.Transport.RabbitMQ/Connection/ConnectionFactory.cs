namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Net.Http;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Net.Http.Headers;
    using global::RabbitMQ.Client;
    using Logging;
    using Support;

    class ConnectionFactory
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(IConnection));

        readonly string endpointName;
        readonly global::RabbitMQ.Client.ConnectionFactory connectionFactory;
        readonly List<AmqpTcpEndpoint> endpoints = [];
        readonly object lockObject = new();

        public ConnectionFactory(string endpointName, ConnectionConfiguration connectionConfiguration, X509Certificate2Collection clientCertificateCollection, bool disableRemoteCertificateValidation, bool useExternalAuthMechanism, TimeSpan heartbeatInterval, TimeSpan networkRecoveryInterval, List<(string hostName, int port, bool useTls)> additionalClusterNodes)
        {
            if (endpointName is null)
            {
                throw new ArgumentNullException(nameof(endpointName));
            }

            if (endpointName == string.Empty)
            {
                throw new ArgumentException("The endpoint name cannot be empty.", nameof(endpointName));
            }

            this.endpointName = endpointName;

            connectionFactory = new global::RabbitMQ.Client.ConnectionFactory
            {
                VirtualHost = connectionConfiguration.VirtualHost,
                UserName = connectionConfiguration.UserName,
                Password = connectionConfiguration.Password,
                RequestedHeartbeat = heartbeatInterval,
                NetworkRecoveryInterval = networkRecoveryInterval,
                DispatchConsumersAsync = true,
            };

            if (useExternalAuthMechanism)
            {
                connectionFactory.AuthMechanisms = new[] { new ExternalMechanismFactory() };
            }

            SetClientProperties(endpointName, connectionConfiguration.UserName);

            var endpoint = CreateAmqpTcpEndpoint(connectionConfiguration.Host, connectionConfiguration.Port, connectionConfiguration.UseTls, clientCertificateCollection, disableRemoteCertificateValidation);
            endpoints.Add(endpoint);

            if (additionalClusterNodes?.Count > 0)
            {
                foreach (var (hostName, port, useTls) in additionalClusterNodes)
                {
                    endpoint = CreateAmqpTcpEndpoint(hostName, port, useTls, clientCertificateCollection, disableRemoteCertificateValidation);
                    endpoints.Add(endpoint);
                }
            }
        }

        static AmqpTcpEndpoint CreateAmqpTcpEndpoint(string hostName, int port, bool useTls, X509Certificate2Collection certificateCollection, bool disableRemoteCertificateValidation)
        {
            var sslOption = new SslOption();

            if (useTls)
            {
                sslOption.ServerName = hostName;
                sslOption.Certs = certificateCollection;
                sslOption.Enabled = useTls;

                if (disableRemoteCertificateValidation)
                {
                    sslOption.AcceptablePolicyErrors = SslPolicyErrors.RemoteCertificateChainErrors |
                                                       SslPolicyErrors.RemoteCertificateNameMismatch |
                                                       SslPolicyErrors.RemoteCertificateNotAvailable;
                }
            }

            return new AmqpTcpEndpoint(hostName, port, sslOption);
        }

        void SetClientProperties(string endpointName, string userName)
        {
            connectionFactory.ClientProperties.Clear();

            var nsbVersion = FileVersionInfo.GetVersionInfo(typeof(Endpoint).Assembly.Location);
            var nsbFileVersion = $"{nsbVersion.FileMajorPart}.{nsbVersion.FileMinorPart}.{nsbVersion.FileBuildPart}";

            var rabbitMQVersion = FileVersionInfo.GetVersionInfo(typeof(ConnectionFactory).Assembly.Location);
            var rabbitMQFileVersion = $"{rabbitMQVersion.FileMajorPart}.{rabbitMQVersion.FileMinorPart}.{rabbitMQVersion.FileBuildPart}";

            var applicationNameAndPath = Environment.GetCommandLineArgs()[0];
            var applicationName = Path.GetFileName(applicationNameAndPath);
            var applicationPath = Path.GetDirectoryName(applicationNameAndPath);

            var hostname = RuntimeEnvironment.MachineName;

            connectionFactory.ClientProperties.Add("client_api", "NServiceBus");
            connectionFactory.ClientProperties.Add("nservicebus_version", nsbFileVersion);
            connectionFactory.ClientProperties.Add("nservicebus.rabbitmq_version", rabbitMQFileVersion);
            connectionFactory.ClientProperties.Add("application", applicationName);
            connectionFactory.ClientProperties.Add("application_location", applicationPath);
            connectionFactory.ClientProperties.Add("machine_name", hostname);
            connectionFactory.ClientProperties.Add("user", userName);
            connectionFactory.ClientProperties.Add("endpoint_name", endpointName);
        }

        public IConnection CreatePublishConnection() => CreateConnection($"{endpointName} Publish", false);

        public IConnection CreateAdministrationConnection() => CreateConnection($"{endpointName} Administration", false);

        public HttpClient CreateManagementClient()
        {
            var client = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:15672/api/")
            };
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{connectionFactory.UserName}:{connectionFactory.Password}")));

            return client;
        }

        public IConnection CreateConnection(string connectionName, bool automaticRecoveryEnabled = true, int consumerDispatchConcurrency = 1)
        {
            lock (lockObject)
            {
                connectionFactory.AutomaticRecoveryEnabled = automaticRecoveryEnabled;
                connectionFactory.ConsumerDispatchConcurrency = consumerDispatchConcurrency;

                var connection = connectionFactory.CreateConnection(endpoints, connectionName);

                connection.ConnectionBlocked += (sender, e) => Logger.WarnFormat("'{0}' connection blocked: {1}", connectionName, e.Reason);
                connection.ConnectionUnblocked += (sender, e) => Logger.WarnFormat("'{0}' connection unblocked}", connectionName);

                connection.ConnectionShutdown += (sender, e) =>
                {
                    if (e.Initiator == ShutdownInitiator.Application && e.ReplyCode == 200)
                    {
                        return;
                    }

                    Logger.WarnFormat("'{0}' connection shutdown: {1}", connectionName, e);
                };

                return connection;
            }
        }
    }
}
