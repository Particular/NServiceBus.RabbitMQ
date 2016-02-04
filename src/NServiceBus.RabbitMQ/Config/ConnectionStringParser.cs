namespace NServiceBus.Transports.RabbitMQ.Config
{
    using Settings;
    using System;
    using System.ComponentModel;
    using System.Data.Common;
    using System.Linq;
    using System.Reflection;

    class ConnectionStringParser : DbConnectionStringBuilder
    {
        readonly ReadOnlySettings settings;

        public ConnectionStringParser(ReadOnlySettings settings)
        {
            this.settings = settings;
        }

        public ConnectionConfiguration Parse(string connectionString)
        {
            ConnectionString = connectionString;

            var connectionConfiguration = new ConnectionConfiguration();
            var connectionConfigurationType = typeof(ConnectionConfiguration);

            foreach (var key in Keys.Cast<string>())
            {
                var property = connectionConfigurationType.GetProperty(key, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                property?.SetValue(connectionConfiguration, TypeDescriptor.GetConverter(property.PropertyType).ConvertFrom(this[key]));
            }

            if (ContainsKey("host"))
            {
                connectionConfiguration.ParseHosts(this["host"] as string);
            }

            if (ContainsKey("dequeuetimeout"))
            {
                var message = "The 'DequeueTimeout' configuration setting has been removed. Please consult the documentation for further information.";
                throw new NotSupportedException(message);
            }

            connectionConfiguration.ClientProperties["endpoint_name"] = settings.EndpointName().ToString();

            connectionConfiguration.Validate();

            return connectionConfiguration;
        }
    }
}