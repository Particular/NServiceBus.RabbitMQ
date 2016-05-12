namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Security.Authentication;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;

    class ConnectionFactory
    {
        readonly global::RabbitMQ.Client.ConnectionFactory connectionFactory;

        public ConnectionFactory(ConnectionConfiguration connectionConfiguration)
        {
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
                RequestedHeartbeat = connectionConfiguration.RequestedHeartbeat,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = connectionConfiguration.RetryDelay
            };

            connectionFactory.Ssl.ServerName = connectionConfiguration.Host;
            connectionFactory.Ssl.CertPath = connectionConfiguration.CertPath;
            connectionFactory.Ssl.CertPassphrase = connectionConfiguration.CertPassphrase;
            connectionFactory.Ssl.Version = SslProtocols.Tls12;
            connectionFactory.Ssl.Enabled = connectionConfiguration.UseTls;

            connectionFactory.ClientProperties.Clear();

            foreach (var item in connectionConfiguration.ClientProperties)
            {
                connectionFactory.ClientProperties.Add(item.Key, item.Value);
            }
        }

        public IConnection CreateConnection(string purpose)
        {
            connectionFactory.ClientProperties["purpose"] = purpose;
            connectionFactory.ClientProperties["connected"] = DateTime.Now.ToString("G");

            return connectionFactory.CreateConnection();
        }
    }
}
