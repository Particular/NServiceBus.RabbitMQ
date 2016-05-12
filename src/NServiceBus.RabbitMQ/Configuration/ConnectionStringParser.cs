namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.ComponentModel;
    using System.Data.Common;
    using System.Linq;
    using System.Reflection;
    using Logging;
    using Settings;

    class ConnectionStringParser : DbConnectionStringBuilder
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(ConnectionStringParser));

        readonly ReadOnlySettings settings;

        public ConnectionStringParser(ReadOnlySettings settings)
        {
            this.settings = settings;
        }

        public ConnectionConfiguration Parse(string connectionString)
        {
            ConnectionString = connectionString;

            var connectionConfiguration = new ConnectionConfiguration(settings);
            var connectionConfigurationType = typeof(ConnectionConfiguration);

            foreach (var key in Keys.Cast<string>())
            {
                var property = connectionConfigurationType.GetProperty(key, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                property?.SetValue(connectionConfiguration, TypeDescriptor.GetConverter(property.PropertyType).ConvertFrom(this[key]));
            }

            if (connectionConfiguration.UseTls && !ContainsKey("port"))
            {
                connectionConfiguration.Port = 5671;
            }

            if (ContainsKey("host"))
            {
                ParseHosts(connectionConfiguration, this["host"] as string);
            }
            else
            {
                throw new Exception("Invalid connection string. 'host' value must be supplied. e.g: \"host=myServer\"");
            }

            if (ContainsKey("dequeuetimeout"))
            {
                var message = "The 'DequeueTimeout' connection string option has been removed. Consult the documentation for further information.";

                Logger.Error(message);

                throw new NotSupportedException(message);
            }

            if (ContainsKey("maxwaittimeforconfirms"))
            {
                var message = "The 'MaxWaitTimeForConfirms' connection string option has been removed. Consult the documentation for further information";

                Logger.Error(message);

                throw new NotSupportedException(message);
            }

            if (ContainsKey("prefetchcount"))
            {
                var message = "The 'PrefetchCount' connection string option has been removed. Use 'EndpointConfiguration.LimitMessageProcessingConcurrencyTo' instead.";

                Logger.Error(message);

                throw new NotSupportedException(message);
            }

            return connectionConfiguration;
        }

        void ParseHosts(ConnectionConfiguration connectionConfiguration, string hostsConnectionString)
        {
            var hostsAndPorts = hostsConnectionString.Split(',');

            if (hostsAndPorts.Length > 1)
            {
                var message =
                    "Multiple hosts are no longer supported. " +
                    "If you are using RabbitMQ in a cluster, " +
                        "consider using a load balancer to represent the nodes as a single host.";

                Logger.Error(message);

                throw new NotSupportedException(message);
            }

            var parts = hostsConnectionString.Split(':');
            connectionConfiguration.Host = parts.ElementAt(0);

            var portString = parts.ElementAtOrDefault(1);
            connectionConfiguration.Port = (portString == null) ? connectionConfiguration.Port : int.Parse(portString);
        }
    }
}
