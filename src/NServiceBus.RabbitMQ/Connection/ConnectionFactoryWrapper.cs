namespace NServiceBus.Transports.RabbitMQ.Connection
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using EasyNetQ;
    using global::RabbitMQ.Client;
    using NServiceBus.Transports.RabbitMQ.Config;

    class ConnectionFactoryWrapper : IConnectionFactory
    {
        public IConnectionConfiguration Configuration { get; private set; }
     
        public ConnectionFactoryWrapper(IConnectionConfiguration connectionConfiguration, IClusterHostSelectionStrategy<ConnectionFactoryInfo> clusterHostSelectionStrategy)
        {
            this.clusterHostSelectionStrategy = clusterHostSelectionStrategy;
            if (connectionConfiguration == null)
            {
                throw new ArgumentNullException("connectionConfiguration");
            }
            if (!connectionConfiguration.Hosts.Any())
            {
                throw new Exception("At least one host must be defined in connectionConfiguration");
            }

            Configuration = connectionConfiguration;

            foreach (var hostConfiguration in Configuration.Hosts)
            {
                clusterHostSelectionStrategy.Add(new ConnectionFactoryInfo(new ConnectionFactory
                    {
                        HostName = hostConfiguration.Host,
                        Port = hostConfiguration.Port,
                        VirtualHost = Configuration.VirtualHost,
                        UserName = Configuration.UserName,
                        Password = Configuration.Password,
                        RequestedHeartbeat = Configuration.RequestedHeartbeat,
                        ClientProperties = ConvertToHashtable(Configuration.ClientProperties)
                    }, hostConfiguration));
            }
        }

        

        public virtual IConnection CreateConnection()
        {
            var connectionFactoryInfo = clusterHostSelectionStrategy.Current();
            var connectionFactory = connectionFactoryInfo.ConnectionFactory;

            return connectionFactory.CreateConnection();
        }

        public virtual IHostConfiguration CurrentHost
        {
            get { return clusterHostSelectionStrategy.Current().HostConfiguration; }
        }

        public virtual bool Next()
        {
            return clusterHostSelectionStrategy.Next();
        }

        public virtual void Reset()
        {
            clusterHostSelectionStrategy.Reset();
        }

        public virtual void Success()
        {
            clusterHostSelectionStrategy.Success();
        }

        public virtual bool Succeeded
        {
            get { return clusterHostSelectionStrategy.Succeeded; }
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

        readonly IClusterHostSelectionStrategy<ConnectionFactoryInfo> clusterHostSelectionStrategy;

    }
}