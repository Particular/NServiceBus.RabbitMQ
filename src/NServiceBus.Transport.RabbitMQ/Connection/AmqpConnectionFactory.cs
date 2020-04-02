namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Net.Security;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using global::RabbitMQ.Client;
    using Logging;

    class AmqpConnectionFactory
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(IConnection));

        readonly string endpointName;
        readonly ConnectionFactory connectionFactory;
        readonly object lockObject = new object();

        public AmqpConnectionFactory(string endpointName,
            string connectionString,
            X509CertificateCollection clientCertificates,
            bool disableRemoteCertificateValidation,
            bool useExternalAuthMechanism,
            ushort requestedHeartbeat,
            TimeSpan retryDelay)
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

            if (connectionString == null)
            {
                throw new ArgumentNullException(nameof(connectionString));
            }

            if (endpointName == string.Empty)
            {
                throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));
            }

            connectionFactory = new ConnectionFactory
            {
                // HostName, Port, VirtualHost, UserName, and Password are set via connection string
                RequestedHeartbeat = requestedHeartbeat,
                NetworkRecoveryInterval = retryDelay,
                UseBackgroundThreadsForIO = true
            };

            connectionFactory.Uri = new Uri(connectionString);
            // connectionFactory.Ssl.Certs = clientCertificates;
            // connectionFactory.Ssl.ServerName = connectionFactory.HostName;
            // connectionFactory.Ssl.CertPath = connectionConfiguration.CertPath;
            // connectionFactory.Ssl.CertPassphrase = connectionConfiguration.CertPassphrase;
            // connectionFactory.Ssl.Version = SslProtocols.Tls12;
            // connectionFactory.Ssl.Enabled = connectionConfiguration.UseTls;
            //
            // if (disableRemoteCertificateValidation)
            // {
            //     connectionFactory.Ssl.AcceptablePolicyErrors = SslPolicyErrors.RemoteCertificateChainErrors |
            //                                                    SslPolicyErrors.RemoteCertificateNameMismatch |
            //                                                    SslPolicyErrors.RemoteCertificateNotAvailable;
            // }
            //
            // if (useExternalAuthMechanism)
            // {
            //     connectionFactory.AuthMechanisms = new[] { new ExternalMechanismFactory() };
            // }
            //
            // connectionFactory.ClientProperties.Clear();
            //
            // foreach (var item in connectionConfiguration.ClientProperties)
            // {
            //     connectionFactory.ClientProperties.Add(item.Key, item.Value);
            // }
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