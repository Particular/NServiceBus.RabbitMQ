namespace NServiceBus.Transports.RabbitMQ.Config
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using NServiceBus.Transports.RabbitMQ.Connection;
    using Support;

    class ConnectionConfiguration
    {
        public const ushort DefaultHeartBeatInSeconds = 5;
        public const int DefaultPort = 5672;

        public static readonly TimeSpan DefaultWaitTimeForConfirms = TimeSpan.FromSeconds(30);

        public string Host { get; set; }

        public int Port { get; set; }

        public string VirtualHost { get; set; }

        public string UserName { get; set; }

        public string Password { get; set; }

        public ushort RequestedHeartbeat { get; set; }

        public bool UsePublisherConfirms { get; set; }

        public TimeSpan MaxWaitTimeForConfirms { get; set; }

        public TimeSpan RetryDelay { get; set; }

        public IDictionary<string, object> ClientProperties { get; } = new Dictionary<string, object>();

        public ConnectionConfiguration()
        {
            // set default values
            Port = DefaultPort;
            VirtualHost = "/";
            UserName = "guest";
            Password = "guest";
            RequestedHeartbeat = DefaultHeartBeatInSeconds;
            MaxWaitTimeForConfirms = DefaultWaitTimeForConfirms;
            RetryDelay = TimeSpan.FromSeconds(10);
            SetDefaultClientProperties();
            UsePublisherConfirms = true;
        }

        private void SetDefaultClientProperties()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            var applicationNameAndPath = Environment.GetCommandLineArgs()[0];
            var applicationName = Path.GetFileName(applicationNameAndPath);
            var applicationPath = Path.GetDirectoryName(applicationNameAndPath);
            var hostname = RuntimeEnvironment.MachineName;

            ClientProperties.Add("client_api", "NServiceBus");
            ClientProperties.Add("nservicebus_version", version);
            ClientProperties.Add("application", applicationName);
            ClientProperties.Add("application_location", applicationPath);
            ClientProperties.Add("machine_name", hostname);
            ClientProperties.Add("user", UserName);
            ClientProperties.Add("connected", DateTime.Now.ToString("G"));
        }

        public void Validate()
        {
            if (Host == null)
            {
                throw new Exception("Invalid connection string. 'host' value must be supplied. e.g: \"host=myServer\"");
            }
        }

        public void ParseHosts(string hostsConnectionString)
        {
            var hostsAndPorts = hostsConnectionString.Split(',');

            if (hostsAndPorts.Length > 1)
            {
                var message =
                    "Multiple hosts are no longer supported. " +
                    "If you are using RabbitMQ in a cluster, " +
                        "consider using a load balancer to represent the nodes as a single host.";

                throw new ArgumentException(message, nameof(hostsConnectionString));
            }

            var parts = hostsConnectionString.Split(':');
            Host = parts.ElementAt(0);

            var portString = parts.ElementAtOrDefault(1);
            Port = (portString == null) ? Port : int.Parse(portString);
        }
    }
}
