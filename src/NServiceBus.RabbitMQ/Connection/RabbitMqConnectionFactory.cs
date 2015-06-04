namespace NServiceBus.Transports.RabbitMQ.Connection
{
    using System;
    using global::RabbitMQ.Client;
    using NServiceBus.Transports.RabbitMQ.Config;

    class RabbitMqConnectionFactory
    {
        public ConnectionConfiguration Configuration { get; private set; }

        public RabbitMqConnectionFactory(ConnectionConfiguration connectionConfiguration)
        {
            if (connectionConfiguration == null)
            {
                throw new ArgumentNullException("connectionConfiguration");
            }

            if (connectionConfiguration.HostConfiguration == null)
            {
                throw new ArgumentException(
                    "The connectionConfiguration has a null HostConfiguration.", "connectionConfiguration");
            }

            Configuration = connectionConfiguration;

            connectionFactory = new ConnectionFactory
            {
                HostName = connectionConfiguration.HostConfiguration.Host,
                Port = connectionConfiguration.HostConfiguration.Port,
                VirtualHost = Configuration.VirtualHost,
                UserName = Configuration.UserName,
                Password = Configuration.Password,
                RequestedHeartbeat = Configuration.RequestedHeartbeat,
                ClientProperties = Configuration.ClientProperties
            };
        }

        public virtual IConnection CreateConnection(string purpose)
        {
            connectionFactory.ClientProperties["purpose"] = purpose;
            return connectionFactory.CreateConnection();
        }

        readonly ConnectionFactory connectionFactory;
    }
}
