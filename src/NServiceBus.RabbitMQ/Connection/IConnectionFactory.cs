namespace NServiceBus.Transports.RabbitMQ.Connection
{
    using global::RabbitMQ.Client;
    using NServiceBus.Transports.RabbitMQ.Config;

    interface IConnectionFactory
    {
        IConnection CreateConnection(string purpose);
        IConnectionConfiguration Configuration { get; }
        IHostConfiguration CurrentHost { get; }
        bool Next();
        void Success();
        void Reset();
        bool Succeeded { get; }
    }
}