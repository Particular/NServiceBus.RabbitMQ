namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.ComponentModel;
    using System.Data.Common;
    using System.Linq;
    using System.Reflection;
    using System.Text;
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
            var invalidOptionsMessage = new StringBuilder();

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
                ParseHosts(connectionConfiguration, this["host"] as string, invalidOptionsMessage);
            }
            else
            {
                invalidOptionsMessage.AppendLine("Invalid connection string. 'host' value must be supplied. e.g: \"host=myServer\"");
            }

            if (ContainsKey("dequeuetimeout"))
            {
                invalidOptionsMessage.AppendLine("The 'DequeueTimeout' connection string option has been removed. Consult the documentation for further information.");
            }

            if (ContainsKey("maxwaittimeforconfirms"))
            {
                invalidOptionsMessage.AppendLine("The 'MaxWaitTimeForConfirms' connection string option has been removed. Consult the documentation for further information");
            }

            if (ContainsKey("prefetchcount"))
            {
                invalidOptionsMessage.AppendLine("The 'PrefetchCount' connection string option has been removed. Use 'EndpointConfiguration.UseTransport<RabbitMQTransport>().PrefetchCount' instead.");
            }

            if (ContainsKey("usepublisherconfirms"))
            {
                invalidOptionsMessage.AppendLine("The 'UsePublisherConfirms' connection string option has been removed. Use 'EndpointConfiguration.UseTransport<RabbitMQTransport>().UsePublisherConfirms' instead.");
            }

            if (invalidOptionsMessage.Length > 0)
            {
                var message = invalidOptionsMessage.ToString().TrimEnd('\r', '\n');

                Logger.Error(message);

                throw new NotSupportedException(message);
            }

            return connectionConfiguration;
        }

        void ParseHosts(ConnectionConfiguration connectionConfiguration, string hostsConnectionString, StringBuilder invalidOptionsMessage)
        {
            var hostsAndPorts = hostsConnectionString.Split(',');

            if (hostsAndPorts.Length > 1)
            {
                invalidOptionsMessage.AppendLine("Multiple hosts are no longer supported. If using RabbitMQ in a cluster, consider using a load balancer to represent the nodes as a single host.");

                return;
            }

            var parts = hostsConnectionString.Split(':');
            connectionConfiguration.Host = parts.ElementAt(0);

            var portString = parts.ElementAtOrDefault(1);
            connectionConfiguration.Port = (portString == null) ? connectionConfiguration.Port : int.Parse(portString);
        }
    }
}
