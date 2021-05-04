namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Security;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using global::RabbitMQ.Client;
    using Logging;
    using Support;

    class ConnectionFactory
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(IConnection));

        readonly string endpointName;
        readonly global::RabbitMQ.Client.ConnectionFactory connectionFactory;
        readonly object lockObject = new object();
        List<AmqpTcpEndpoint> hostnames;

        public ConnectionFactory(string endpointName, string host, int port, string vhost, string userName, string password, bool useTls, X509Certificate2Collection clientCertificateCollection, bool validateRemoteCertificate, bool useExternalAuthMechanism, TimeSpan heartbeatInterval, TimeSpan networkRecoveryInterval, List<string> additionalHostnames)
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
                HostName = host,
                Port = port,
                VirtualHost = vhost,
                UserName = userName,
                Password = password,
                RequestedHeartbeat = heartbeatInterval,
                NetworkRecoveryInterval = networkRecoveryInterval,
                UseBackgroundThreadsForIO = true,
                DispatchConsumersAsync = true
            };

            connectionFactory.Ssl.ServerName = host;
            connectionFactory.Ssl.Certs = clientCertificateCollection;
            connectionFactory.Ssl.Version = SslProtocols.Tls12;
            connectionFactory.Ssl.Enabled = useTls;

            if (!validateRemoteCertificate)
            {
                connectionFactory.Ssl.AcceptablePolicyErrors = SslPolicyErrors.RemoteCertificateChainErrors |
                                                               SslPolicyErrors.RemoteCertificateNameMismatch |
                                                               SslPolicyErrors.RemoteCertificateNotAvailable;
            }

            if (useExternalAuthMechanism)
            {
                connectionFactory.AuthMechanisms = new[] { new ExternalMechanismFactory() };
            }

            SetClientProperties(endpointName, userName);

            hostnames = new List<AmqpTcpEndpoint>
            {
                AmqpTcpEndpoint.Parse($"{host}:{port}")
            };

            if (additionalHostnames?.Count > 0)
            {
                hostnames.AddRange(additionalHostnames.Select(AmqpTcpEndpoint.Parse));
            }
        }

        void SetClientProperties(string endpointName, string userName)
        {
            connectionFactory.ClientProperties.Clear();

            var nsbVersion = FileVersionInfo.GetVersionInfo(typeof(Endpoint).Assembly.Location);
            var nsbFileVersion = $"{nsbVersion.FileMajorPart}.{nsbVersion.FileMinorPart}.{nsbVersion.FileBuildPart}";

            var rabbitMQVersion = FileVersionInfo.GetVersionInfo(typeof(ConnectionFactory).Assembly.Location);
            var rabbitMQFileVersion =
                $"{rabbitMQVersion.FileMajorPart}.{rabbitMQVersion.FileMinorPart}.{rabbitMQVersion.FileBuildPart}";

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

        public IConnection CreateConnection(string connectionName, bool automaticRecoveryEnabled = true, int consumerDispatchConcurrency = 1)
        {
            lock (lockObject)
            {
                connectionFactory.AutomaticRecoveryEnabled = automaticRecoveryEnabled;
                connectionFactory.ConsumerDispatchConcurrency = consumerDispatchConcurrency;

                var connection = connectionFactory.CreateConnection(hostnames, connectionName);

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
