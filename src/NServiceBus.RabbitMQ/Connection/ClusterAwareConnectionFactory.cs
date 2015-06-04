namespace NServiceBus.Transports.RabbitMQ.Connection
{
    using System;
    using System.Collections.Generic;
    using global::RabbitMQ.Client;
    using NServiceBus.Transports.RabbitMQ.Config;

    class ClusterAwareConnectionFactory
    {
        readonly ConnectionFactoryInfo connectionFactoryInfo;

        public ConnectionConfiguration Configuration { get; private set; }

        public ClusterAwareConnectionFactory(
            ConnectionConfiguration connectionConfiguration)
        {
            if (connectionConfiguration == null)
            {
                throw new ArgumentNullException("connectionConfiguration");
            }

            if (connectionConfiguration.HostConfiguration == null)
            {
                throw new Exception("A host must be defined in connectionConfiguration");
            }

            Configuration = connectionConfiguration;

            connectionFactoryInfo = new ConnectionFactoryInfo(
                new ConnectionFactory
                {
                    HostName = connectionConfiguration.HostConfiguration.Host,
                    Port = connectionConfiguration.HostConfiguration.Port,
                    VirtualHost = Configuration.VirtualHost,
                    UserName = Configuration.UserName,
                    Password = Configuration.Password,
                    RequestedHeartbeat = Configuration.RequestedHeartbeat,
                    ClientProperties = ConvertToHashtable(Configuration.ClientProperties)
                },
                connectionConfiguration.HostConfiguration);
        }

        public virtual IConnection CreateConnection(string purpose)
        {
            var connectionFactory = connectionFactoryInfo.ConnectionFactory;

            connectionFactory.ClientProperties["purpose"] = purpose;

            return connectionFactory.CreateConnection();
        }

        public virtual HostConfiguration HostConfiguration
        {
            get { return connectionFactoryInfo.HostConfiguration; }
        }

        static IDictionary<string, object> ConvertToHashtable(IDictionary<string, object> clientProperties)
        {
            var dictionary = new Dictionary<string, object>();
            foreach (var clientProperty in clientProperties)
            {
                dictionary.Add(clientProperty.Key, clientProperty.Value);
            }

            return dictionary;
        }
    }
}
