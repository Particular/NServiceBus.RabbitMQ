namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using global::RabbitMQ.Client;

    class ConnectionManager : IDisposable
    {
        public ConnectionManager(RabbitMqConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public IConnection GetPublishConnection()
        {
            lock (connectionFactory)
            {
                return connectionPublish ?? (connectionPublish = connectionFactory.CreateConnection("Publish"));
            }
        }

        public IConnection CreateAdministrationConnection()
        {
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
        IConnection connectionPublish;
    }
}