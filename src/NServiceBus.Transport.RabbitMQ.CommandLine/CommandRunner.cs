namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using global::RabbitMQ.Client;

    public static class CommandRunner
    {
        public static void Run(string connectionString, X509Certificate2? certificate, Action<IModel> command)
        {
            var connectionConfiguration = ConnectionConfiguration.Create(connectionString);

            var factory = new ConnectionFactory
            {
                HostName = connectionConfiguration.Host,
                Port = connectionConfiguration.Port,
                VirtualHost = connectionConfiguration.VirtualHost,
                UserName = connectionConfiguration.UserName,
                Password = connectionConfiguration.Password,
                UseBackgroundThreadsForIO = true,
                DispatchConsumersAsync = true,
            };

            if (certificate != null && connectionConfiguration.UseTls)
            {
                factory.Ssl.ServerName = connectionConfiguration.Host;
                factory.Ssl.Certs = new X509CertificateCollection(new X509Certificate2[] { certificate });
                factory.Ssl.Version = SslProtocols.Tls12;
                factory.Ssl.Enabled = connectionConfiguration.UseTls;
            }

            using (IConnection connection = factory.CreateConnection("rabbitmq-transport"))
            {
                using (IModel channel = connection.CreateModel())
                {
                    command(channel);
                    channel.Close();
                }

                connection.Close();
            }
        }
    }
}