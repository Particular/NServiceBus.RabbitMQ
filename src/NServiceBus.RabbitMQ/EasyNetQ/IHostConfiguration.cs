namespace EasyNetQ
{
    interface IHostConfiguration
    {
        string Host { get; }
        ushort Port { get; }
    }
}