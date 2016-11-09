namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using Settings;
    using Support;

    static class DefaultConfigurationValues
    {
        public static void Apply(SettingsHolder settings)
        {
            // set default values
            settings.SetDefault(SettingsKeys.Port, PortDefault);
            settings.SetDefault(SettingsKeys.VirtualHost, VirtualHostDefault);
            settings.SetDefault(SettingsKeys.UserName, UserNameDefault);
            settings.SetDefault(SettingsKeys.Password, PasswordDefault);
            settings.SetDefault(SettingsKeys.RequestedHeartbeat, RequestedHeartbeatDefault);
            settings.SetDefault(SettingsKeys.RetryDelay, RetryDelayDefault);
            settings.SetDefault(SettingsKeys.UseTls, UseTlsDefault);
            settings.SetDefault(SettingsKeys.CertPath, CertPathDefault);
            settings.SetDefault(SettingsKeys.CertPassphrase, CertPassphraseDefault);
            settings.SetDefault(SettingsKeys.TimeToWaitBeforeTriggeringCircuitBreaker, TimeToWaitBeforeTriggeringCircuitBreakerDefault);
            settings.SetDefault(SettingsKeys.PrefetchMultiplier, PrefetchMultiplierDefault);
            settings.SetDefault(SettingsKeys.PrefetchCount, PrefetchCountDefault);
            settings.SetDefault(SettingsKeys.UsePublisherConfirms, UsePublisherConfirmsDefault);
            settings.SetDefault<Func<bool, IRoutingTopology>>(RoutingTopologyDefault);
            settings.SetDefault("NServiceBus.HostInformation.DisplayName", DisplayNameDefault);
            SetDefaultClientProperties(settings);
        }

        static readonly int PortDefault = 5672;
        static readonly string VirtualHostDefault = "/";
        static readonly string UserNameDefault = "guest";
        static readonly string PasswordDefault = "guest";
        static readonly ushort RequestedHeartbeatDefault = 5;
        static readonly TimeSpan RetryDelayDefault = TimeSpan.FromSeconds(10);
        static readonly bool UseTlsDefault = false;
        static readonly string CertPathDefault = "";
        static readonly string CertPassphraseDefault = string.Empty;
        static readonly TimeSpan TimeToWaitBeforeTriggeringCircuitBreakerDefault = TimeSpan.FromMinutes(2);
        static readonly bool UsePublisherConfirmsDefault = true;
        static readonly ushort PrefetchCountDefault = 0;
        static readonly int PrefetchMultiplierDefault = 3;
        static readonly Func<bool, IRoutingTopology> RoutingTopologyDefault = d => new ConventionalRoutingTopology(d);
        static readonly string DisplayNameDefault = Support.RuntimeEnvironment.MachineName;

        static void SetDefaultClientProperties(SettingsHolder settings)
        {
            var nsb = typeof(Endpoint).Assembly.Location;
            var nsbVersion = FileVersionInfo.GetVersionInfo(nsb);
            var nsbFileVersion = $"{nsbVersion.FileMajorPart}.{nsbVersion.FileMinorPart}.{nsbVersion.FileBuildPart}";

            var rabbitMQ = typeof(DefaultConfigurationValues).Assembly.Location;
            var rabbitMQVersion = FileVersionInfo.GetVersionInfo(rabbitMQ);
            var rabbitMQFileVersion = $"{rabbitMQVersion.FileMajorPart}.{rabbitMQVersion.FileMinorPart}.{rabbitMQVersion.FileBuildPart}";

            var applicationNameAndPath = Environment.GetCommandLineArgs()[0];
            var applicationName = Path.GetFileName(applicationNameAndPath);
            var applicationPath = Path.GetDirectoryName(applicationNameAndPath);

            var hostname = RuntimeEnvironment.MachineName;

            var clientProperties = new Dictionary<string, object>();

            clientProperties.Add("client_api", "NServiceBus");
            clientProperties.Add("nservicebus_version", nsbFileVersion);
            clientProperties.Add("nservicebus.rabbitmq_version", rabbitMQFileVersion);
            clientProperties.Add("application", applicationName);
            clientProperties.Add("application_location", applicationPath);
            clientProperties.Add("machine_name", hostname);
            clientProperties.Add("user", UserNameDefault);
            clientProperties.Add("endpoint_name", settings.EndpointName());

            settings.Set(SettingsKeys.ClientProperties, clientProperties);
        }
    }
}
