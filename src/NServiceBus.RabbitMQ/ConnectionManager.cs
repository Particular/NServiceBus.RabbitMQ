namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using global::RabbitMQ.Client;
    using NServiceBus.Transports.RabbitMQ.Connection;

    class ConnectionManager : IDisposable
    {
        public ConnectionManager(RabbitMqConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public IConnection GetPublishConnection()
        {
            //note: The purpose is there so that we/users can add more advanced connection managers in the future
            lock (connectionFactory)
            {
                return connectionPublish ?? (connectionPublish = connectionFactory.CreateConnection("Publish"));
            }
        }

        public IConnection GetConsumeConnection()
        {
            //note: The purpose is there so that we/users can add more advanced connection managers in the future
            lock (connectionFactory)
            {
                return connectionConsume ?? (connectionConsume = connectionFactory.CreateConnection("Consume"));
            }
        }

        public IConnection GetAdministrationConnection()
        {
            //note: The purpose is there so that we/users can add more advanced connection managers in the future
            lock (connectionFactory)
            {
                return connectionFactory.CreateConnection("Administration");
            }
        }

        public void Dispose()
        {
            //Injected
        }

        readonly RabbitMqConnectionFactory connectionFactory;
        IConnection connectionConsume;
        IConnection connectionPublish;
    }
}