namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using global::RabbitMQ.Client;

    public static class CommandRunner
    {
        public static void Run(string connectionString, Action<IModel> command)
        {
            var connectionData = ConnectionConfiguration.Create(connectionString);

            var factory = new ConnectionFactory
            {
                HostName = connectionData.Host,
                Port = connectionData.Port,
                VirtualHost = connectionData.VirtualHost,
                UserName = connectionData.UserName,
                Password = connectionData.Password,
                UseBackgroundThreadsForIO = true,
                DispatchConsumersAsync = true
            };

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