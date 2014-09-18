namespace EasyNetQ
{
    class HostConfiguration : IHostConfiguration
    {
        public string Host { get; set; }
        public ushort Port { get; set; }
    }
}