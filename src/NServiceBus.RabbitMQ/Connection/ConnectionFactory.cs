namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Security.Authentication;
    using global::RabbitMQ.Client;
    using Settings;

    class ConnectionFactory
    {
        readonly global::RabbitMQ.Client.ConnectionFactory connectionFactory;
        readonly object lockObject = new object();

        public ConnectionFactory(SettingsHolder settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (settings.Get(SettingsKeys.Host) == null)
            {
                throw new ArgumentException("The connectionConfiguration has a null Host.", nameof(settings));
            }

            connectionFactory = new global::RabbitMQ.Client.ConnectionFactory
            {
                HostName = settings.Get<string>(SettingsKeys.Host),
                Port = settings.Get<int>(SettingsKeys.Port),
                VirtualHost = settings.Get<string>(SettingsKeys.VirtualHost),
                UserName = settings.Get<string>(SettingsKeys.UserName),
                Password = settings.Get<string>(SettingsKeys.Password),
                RequestedHeartbeat = settings.Get<ushort>(SettingsKeys.RequestedHeartbeat),
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = settings.Get<TimeSpan>(SettingsKeys.RetryDelay),
                UseBackgroundThreadsForIO = true
            };

            connectionFactory.Ssl.ServerName = settings.Get<string>(SettingsKeys.Host);
            connectionFactory.Ssl.CertPath = settings.Get<string>(SettingsKeys.CertPath);
            connectionFactory.Ssl.CertPassphrase = settings.Get<string>(SettingsKeys.CertPassphrase);
            connectionFactory.Ssl.Version = SslProtocols.Tls12;
            connectionFactory.Ssl.Enabled = settings.Get<bool>(SettingsKeys.UseTls);

            connectionFactory.ClientProperties.Clear();

            foreach (var item in settings.Get<Dictionary<string, object>>(SettingsKeys.ClientProperties))
            {
                connectionFactory.ClientProperties.Add(item.Key, item.Value);
            }
        }

        public IConnection CreatePublishConnection() => CreateConnection("Publish");

        public IConnection CreateAdministrationConnection() => CreateConnection("Administration");

        public IConnection CreateConnection(string connectionName)
        {
            lock (lockObject)
            {
                connectionFactory.ClientProperties["connected"] = DateTime.Now.ToString("G");

                return connectionFactory.CreateConnection(connectionName);
            }
        }
    }
}
