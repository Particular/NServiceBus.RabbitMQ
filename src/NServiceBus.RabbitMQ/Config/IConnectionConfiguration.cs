namespace NServiceBus.Transports.RabbitMQ.Config
{
    using System;
    using System.Collections.Generic;
    using EasyNetQ;

    interface IConnectionConfiguration
    {
        ushort Port { get; }
        string VirtualHost { get; }
        string UserName { get; }
        string Password { get; }
        ushort RequestedHeartbeat { get; }
        ushort PrefetchCount { get; }
        int DequeueTimeout { get; }
        IDictionary<string, object> ClientProperties { get; } 
        IEnumerable<IHostConfiguration> Hosts { get; }
        TimeSpan RetryDelay { get; set; }
        bool UsePublisherConfirms { get; set; }
        TimeSpan MaxWaitTimeForConfirms { get; set; }
    }
}