namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using global::RabbitMQ.Client;
    using NServiceBus.Transport.RabbitMQ.CommandLine.Configuration;

    public static class CommandRunner
    {
        public static void Run(string connectionString, Action<IModel> command)
        {
            var connectionData = ConnectionSettings.Parse(connectionString);

            var factory = new ConnectionFactory
            {
                HostName = connectionData.Host,
                Port = connectionData.Port,
                VirtualHost = connectionData.VHost,
                UserName = connectionData.UserName,
                Password = connectionData.Password,
                RequestedHeartbeat = connectionData.HeartbeatInterval,
                NetworkRecoveryInterval = connectionData.NetworkRecoveryInterval,
                UseBackgroundThreadsForIO = true,
                DispatchConsumersAsync = true
            };

            using (IConnection connection = factory.CreateConnection("rmq-transport"))
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