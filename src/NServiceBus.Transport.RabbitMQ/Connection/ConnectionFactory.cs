namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using Logging;
    using Support;

    class ConnectionFactory
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(IConnection));

        readonly string endpointName;
        readonly global::RabbitMQ.Client.ConnectionFactory connectionFactory;
        readonly List<AmqpTcpEndpoint> endpoints = [];

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
                // What do we do with the consumer dispatch concurrency?
            };

            if (useExternalAuthMechanism)
            {
                connectionFactory.AuthMechanisms = [new ExternalMechanismFactory()];
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

        public Task<IConnection> CreatePublishConnection(CancellationToken cancellationToken = default) => CreateConnection($"{endpointName} Publish", cancellationToken: cancellationToken);

        public Task<IConnection> CreateAdministrationConnection(CancellationToken cancellationToken = default) => CreateConnection($"{endpointName} Administration", cancellationToken: cancellationToken);

        public async Task<IConnection> CreateConnection(string connectionName, CancellationToken cancellationToken = default)
        {
            var connection = await connectionFactory.CreateConnectionAsync(endpoints, connectionName, cancellationToken).ConfigureAwait(false);

            connection.ConnectionBlockedAsync += (sender, e) =>
            {
                Logger.WarnFormat("'{0}' connection blocked: {1}", connectionName, e.Reason);
                return Task.CompletedTask;
            };
            connection.ConnectionUnblockedAsync += (sender, e) =>
            {
                Logger.WarnFormat("'{0}' connection unblocked}", connectionName);
                return Task.CompletedTask;
            };

            connection.ConnectionShutdownAsync += (sender, e) =>
            {
                if (e.Initiator == ShutdownInitiator.Application && e.ReplyCode == 200)
                {
                    return Task.CompletedTask;
                }

                Logger.WarnFormat("'{0}' connection shutdown: {1}", connectionName, e);

                return Task.CompletedTask;
            };

            return connection;
        }
    }
}
