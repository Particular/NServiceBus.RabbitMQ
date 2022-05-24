namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System.Security.Cryptography.X509Certificates;
    using global::RabbitMQ.Client;

    public static class ConnectionHelper
    {
        public static IConnection GetConnection(string connectionString, X509Certificate2? certificate = null)
        {
            var connectionConfiguration = ConnectionConfiguration.Create(connectionString);
            var certificateCollection = new X509Certificate2Collection();

            if (certificate != null)
            {
                certificateCollection.Add(certificate);
            }

            var connectionFactory = new RabbitMQ.ConnectionFactory("rabbitmq-transport",
                        connectionConfiguration,
                        certificateCollection,
                        false,
                        false,
                        TimeSpan.FromSeconds(10),
                        TimeSpan.FromSeconds(60),
                        new List<(string, int)>());

            return connectionFactory.CreateAdministrationConnection();
        }
    }
}