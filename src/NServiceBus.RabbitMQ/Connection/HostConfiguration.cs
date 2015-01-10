namespace NServiceBus.Transports.RabbitMQ.Connection
{
    class HostConfiguration : IHostConfiguration
    {
        public string Host { get; set; }
        public ushort Port { get; set; }
    }
}