namespace NServiceBus.Transports.RabbitMQ.Connection
{
    interface IHostConfiguration
    {
        string Host { get; }
        ushort Port { get; }
    }
}