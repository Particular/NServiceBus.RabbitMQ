namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using Config;
    using global::RabbitMQ.Client;
    using NServiceBus.Transports.RabbitMQ.Connection;

    class RabbitMqConnectionManager : IDisposable, IManageRabbitMqConnections
    {
        public RabbitMqConnectionManager(IConnectionFactory connectionFactory, IConnectionConfiguration connectionConfiguration)
        {
            this.connectionFactory = connectionFactory;
            this.connectionConfiguration = connectionConfiguration;
        }

        public IConnection GetPublishConnection()
        {
            //note: The purpose is there so that we/users can add more advanced connection managers in the future
            lock (connectionFactory)
            {
                return connectionPublish ?? (connectionPublish = new PersistentConnection(connectionFactory, connectionConfiguration.RetryDelay,"Publish"));
            }
        }

        public IConnection GetConsumeConnection()
        {
            //note: The purpose is there so that we/users can add more advanced connection managers in the future
            lock (connectionFactory)
            {
                return connectionConsume ?? (connectionConsume = new PersistentConnection(connectionFactory, connectionConfiguration.RetryDelay,"Consume"));
            }
        }

        public IConnection GetAdministrationConnection()
        {
            //note: The purpose is there so that we/users can add more advanced connection managers in the future
            lock (connectionFactory)
            {
                return new PersistentConnection(connectionFactory, connectionConfiguration.RetryDelay,"Administration");
            }
        }

        public void Dispose()
        {
            //Injected at compile time
        }

        public void DisposeManaged()
        {

            if (connectionConsume != null)
            {
                connectionConsume.Dispose();
            }
            if (connectionPublish != null)
            {
                connectionPublish.Dispose();
            }
        }

        IConnectionFactory connectionFactory;
        IConnectionConfiguration connectionConfiguration;
        PersistentConnection connectionConsume;
        PersistentConnection connectionPublish;
    }
}