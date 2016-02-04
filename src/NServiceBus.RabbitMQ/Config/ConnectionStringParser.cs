namespace NServiceBus.Transports.RabbitMQ.Config
{
    using System.ComponentModel;
    using System.Data.Common;
    using System.Linq;
    using System.Reflection;
    using Settings;

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

            connectionConfiguration.ClientProperties["endpoint_name"] = settings.EndpointName().ToString();

            connectionConfiguration.Validate();

            return connectionConfiguration;
        }
    }
}