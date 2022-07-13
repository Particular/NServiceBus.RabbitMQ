namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Security;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using global::RabbitMQ.Client;
    using Logging;

    class ConnectionFactory
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(IConnection));

        readonly string endpointName;
        readonly global::RabbitMQ.Client.ConnectionFactory connectionFactory;
        readonly object lockObject = new object();

        public ConnectionFactory(string endpointName, ConnectionConfiguration connectionConfiguration, X509Certificate2Collection clientCertificateCollection, bool disableRemoteCertificateValidation, bool useExternalAuthMechanism, TimeSpan? heartbeatInterval, TimeSpan? networkRecoveryInterval)
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

            if (connectionConfiguration == null)
            {
                throw new ArgumentNullException(nameof(connectionConfiguration));
            }

            if (connectionConfiguration.Host == null)
            {
                throw new ArgumentException("The connectionConfiguration has a null Host.", nameof(connectionConfiguration));
            }

            connectionFactory = new global::RabbitMQ.Client.ConnectionFactory
            {
                HostName = connectionConfiguration.Host,
                Port = connectionConfiguration.Port,
                VirtualHost = connectionConfiguration.VirtualHost,
                UserName = connectionConfiguration.UserName,
                Password = connectionConfiguration.Password,
                RequestedHeartbeat = heartbeatInterval ?? connectionConfiguration.RequestedHeartbeat,
                NetworkRecoveryInterval = networkRecoveryInterval ?? connectionConfiguration.RetryDelay,
                UseBackgroundThreadsForIO = true
            };

            connectionFactory.Ssl.ServerName = connectionConfiguration.Host;
            connectionFactory.Ssl.Certs = clientCertificateCollection;
            connectionFactory.Ssl.CertPath = connectionConfiguration.CertPath;
            connectionFactory.Ssl.CertPassphrase = connectionConfiguration.CertPassphrase;
            connectionFactory.Ssl.Version = SslProtocols.Tls12;
            connectionFactory.Ssl.Enabled = connectionConfiguration.UseTls;

            if (disableRemoteCertificateValidation)
            {
                connectionFactory.Ssl.AcceptablePolicyErrors = SslPolicyErrors.RemoteCertificateChainErrors |
                                                               SslPolicyErrors.RemoteCertificateNameMismatch |
                                                               SslPolicyErrors.RemoteCertificateNotAvailable;
            }

            if (useExternalAuthMechanism)
            {
                connectionFactory.AuthMechanisms = new[] { new ExternalMechanismFactory() };
            }

            SetClientProperties(endpointName, connectionConfiguration.UserName);
        }

        void SetClientProperties(string endpointName, string userName)
        {
            connectionFactory.ClientProperties.Clear();

            var nsbVersion = FileVersionInfo.GetVersionInfo(typeof(Endpoint).Assembly.Location);
            var nsbFileVersion = $"{nsbVersion.FileMajorPart}.{nsbVersion.FileMinorPart}.{nsbVersion.FileBuildPart}";

            var rabbitMQVersion = FileVersionInfo.GetVersionInfo(typeof(ConnectionConfiguration).Assembly.Location);
            var rabbitMQFileVersion = $"{rabbitMQVersion.FileMajorPart}.{rabbitMQVersion.FileMinorPart}.{rabbitMQVersion.FileBuildPart}";

            var applicationNameAndPath = Environment.GetCommandLineArgs()[0];
            var applicationName = Path.GetFileName(applicationNameAndPath);
            var applicationPath = Path.GetDirectoryName(applicationNameAndPath);

            var hostname = Environment.MachineName;

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

        public IConnection CreateConnection(string connectionName, bool automaticRecoveryEnabled = true)
        {
            lock (lockObject)
            {
                connectionFactory.AutomaticRecoveryEnabled = automaticRecoveryEnabled;
                connectionFactory.ClientProperties["connected"] = DateTime.Now.ToString("G");

                var connection = connectionFactory.CreateConnection(connectionName);

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
