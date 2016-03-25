namespace NServiceBus.Transports.RabbitMQ.Config
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using NServiceBus.Settings;
    using NServiceBus.Support;

    class ConnectionConfiguration
    {
        public string Host { get; set; }

        public int Port { get; set; }

        public string VirtualHost { get; set; }

        public string UserName { get; set; }

        public string Password { get; set; }

        public ushort RequestedHeartbeat { get; set; }

        public bool UsePublisherConfirms { get; set; }

        public TimeSpan MaxWaitTimeForConfirms { get; set; }

        public TimeSpan RetryDelay { get; set; }

        public bool UseTls { get; set; }

        public string CertPath { get; set; }

        public string CertPassphrase { get; set; }

        public IDictionary<string, object> ClientProperties { get; } = new Dictionary<string, object>();

        public ConnectionConfiguration(ReadOnlySettings settings)
        {
            // set default values
            Port = 5672;
            VirtualHost = "/";
            UserName = "guest";
            Password = "guest";
            RequestedHeartbeat = 5;
            UsePublisherConfirms = true;
            MaxWaitTimeForConfirms = TimeSpan.FromSeconds(30);
            RetryDelay = TimeSpan.FromSeconds(10);
            UseTls = false;
            CertPath = "";
            CertPassphrase = null;

            SetDefaultClientProperties(settings);
        }

        void SetDefaultClientProperties(ReadOnlySettings settings)
        {
            var nsb = typeof(Endpoint).Assembly.Location;
            var nsbVersion = FileVersionInfo.GetVersionInfo(nsb);
            var nsbFileVersion = $"{nsbVersion.FileMajorPart}.{nsbVersion.FileMinorPart}.{nsbVersion.FileBuildPart}";

            var rabbitMQ = typeof(ConnectionConfiguration).Assembly.Location;
            var rabbitMQVersion = FileVersionInfo.GetVersionInfo(rabbitMQ);
            var rabbitMQFileVersion = $"{rabbitMQVersion.FileMajorPart}.{rabbitMQVersion.FileMinorPart}.{rabbitMQVersion.FileBuildPart}";

            var applicationNameAndPath = Environment.GetCommandLineArgs()[0];
            var applicationName = Path.GetFileName(applicationNameAndPath);
            var applicationPath = Path.GetDirectoryName(applicationNameAndPath);

            var hostname = RuntimeEnvironment.MachineName;

            ClientProperties.Add("client_api", "NServiceBus");
            ClientProperties.Add("nservicebus_version", nsbFileVersion);
            ClientProperties.Add("nservicebus.rabbitmq_version", rabbitMQFileVersion);
            ClientProperties.Add("application", applicationName);
            ClientProperties.Add("application_location", applicationPath);
            ClientProperties.Add("machine_name", hostname);
            ClientProperties.Add("user", UserName);
            ClientProperties.Add("endpoint_name", settings.EndpointName().ToString());
        }
    }
}
