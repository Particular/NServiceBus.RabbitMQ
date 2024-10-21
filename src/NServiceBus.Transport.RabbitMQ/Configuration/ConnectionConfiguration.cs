namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Configuration container for the RabbitMQ connection.
    /// </summary>
    public class ConnectionConfiguration
    {
        const bool defaultUseTls = false;
        const int defaultPort = 5672;
        const int defaultTlsPort = 5671;
        const string defaultVirtualHost = "/";
        const string defaultUserName = "guest";
        const string defaultPassword = "guest";

        /// <summary>
        /// Gets the broker host.
        /// </summary>
        public string Host { get; }

        /// <summary>
        /// Gets the port the broker uses.
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// Gets the virtual host.
        /// </summary>
        public string VirtualHost { get; }

        /// <summary>
        /// Gets the user name used for the connection.
        /// </summary>
        public string UserName { get; }

        /// <summary>
        /// Gets the password used for the connection.
        /// </summary>
        public string Password { get; }

        /// <summary>
        /// Gets whether TLS should be used.
        /// </summary>
        public bool UseTls { get; }

        ConnectionConfiguration(
            string host,
            int port,
            string virtualHost,
            string userName,
            string password,
            bool useTls)
        {
            Host = host;
            Port = port;
            VirtualHost = virtualHost;
            UserName = userName;
            Password = password;
            UseTls = useTls;
        }

        /// <summary>
        /// Parses the connection string and returns the connection information.
        /// </summary>
        /// <param name="connectionString">The connection string to be parsed.</param>
        /// <returns></returns>        
        public static ConnectionConfiguration Create(string connectionString)
        {
            Dictionary<string, string> dictionary;
            var invalidOptionsMessage = new StringBuilder();

            if (connectionString.StartsWith("amqp", StringComparison.OrdinalIgnoreCase))
            {
                dictionary = ParseAmqpConnectionString(connectionString, invalidOptionsMessage);
            }
            else
            {
                dictionary = ParseNServiceBusConnectionString(connectionString, invalidOptionsMessage);
            }

            var host = GetValue(dictionary, "host", string.Empty);
            var useTls = GetValue(dictionary, "useTls", bool.TryParse, defaultUseTls, invalidOptionsMessage);
            var port = GetValue(dictionary, "port", int.TryParse, useTls ? defaultTlsPort : defaultPort, invalidOptionsMessage);
            var virtualHost = GetValue(dictionary, "virtualHost", defaultVirtualHost);
            var userName = GetValue(dictionary, "userName", defaultUserName);
            var password = GetValue(dictionary, "password", defaultPassword);

            if (invalidOptionsMessage.Length > 0)
            {
                throw new NotSupportedException(invalidOptionsMessage.ToString().TrimEnd('\r', '\n'));
            }

            return new ConnectionConfiguration(host, port, virtualHost, userName, password, useTls);
        }

        static Dictionary<string, string> ParseAmqpConnectionString(string connectionString, StringBuilder invalidOptionsMessage)
        {
            var dictionary = new Dictionary<string, string>();
            var uri = new Uri(connectionString);

            var usingTls = string.Equals("amqps", uri.Scheme, StringComparison.OrdinalIgnoreCase) ? bool.TrueString : bool.FalseString;
            dictionary.Add("useTls", usingTls);

            dictionary.Add("host", uri.Host);

            if (!uri.IsDefaultPort)
            {
                dictionary.Add("port", uri.Port.ToString());
            }

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var userPass = uri.UserInfo.Split(':');

                if (userPass.Length > 2)
                {
                    invalidOptionsMessage.AppendLine($"Bad user info in AMQP URI: {uri.UserInfo}");
                }
                else
                {
                    dictionary.Add("userName", UriDecode(userPass[0]));

                    if (userPass.Length == 2)
                    {
                        dictionary.Add("password", UriDecode(userPass[1]));
                    }
                }
            }

            if (uri.Segments.Length > 2)
            {
                invalidOptionsMessage.AppendLine($"Multiple segments in path of AMQP URI: {string.Join(", ", uri.Segments)}");
            }
            else if (uri.Segments.Length == 2)
            {
                dictionary.Add("virtualHost", UriDecode(uri.Segments[1]));
            }

            return dictionary;
        }

        static Dictionary<string, string> ParseNServiceBusConnectionString(string connectionString, StringBuilder invalidOptionsMessage)
        {
            var dictionary = new DbConnectionStringBuilder { ConnectionString = connectionString }
                .OfType<KeyValuePair<string, object>>()
                .ToDictionary(pair => pair.Key, pair => pair.Value.ToString() ?? string.Empty, StringComparer.OrdinalIgnoreCase);

            RegisterDeprecatedSettingsAsInvalidOptions(dictionary, invalidOptionsMessage);

            if (dictionary.TryGetValue("port", out var portValue) && !int.TryParse(portValue, out var port))
            {
                invalidOptionsMessage.AppendLine($"'{portValue}' is not a valid Int32 value for the 'port' connection string option.");
            }

            if (dictionary.TryGetValue("host", out var value))
            {
                var firstHostAndPort = value.Split(',')[0];
                var parts = firstHostAndPort.Split(':');
                var host = parts.ElementAt(0);

                if (host.Length == 0)
                {
                    invalidOptionsMessage.AppendLine("Empty host name in 'host' connection string option.");
                }

                dictionary["host"] = host;

                if (parts.Length > 1)
                {
                    if (!int.TryParse(parts[1], out port))
                    {
                        invalidOptionsMessage.AppendLine($"'{parts[1]}' is not a valid Int32 value for the port in the 'host' connection string option.");
                    }
                    else
                    {
                        dictionary["port"] = port.ToString();
                    }
                }
            }
            else
            {
                invalidOptionsMessage.AppendLine("Invalid connection string. 'host' value must be supplied. e.g: \"host=myServer\"");
            }

            return dictionary;
        }

        static void RegisterDeprecatedSettingsAsInvalidOptions(Dictionary<string, string> dictionary, StringBuilder invalidOptionsMessage)
        {
            if (dictionary.TryGetValue("host", out var value))
            {
                var hostsAndPorts = value.Split(',');

                if (hostsAndPorts.Length > 1)
                {
                    invalidOptionsMessage.AppendLine("Multiple hosts are no longer supported in the connection string. Use 'EndpointConfiguration.UseTransport<RabbitMQTransport>().AddClusterNode' instead.");
                }
            }

            if (dictionary.ContainsKey("dequeuetimeout"))
            {
                invalidOptionsMessage.AppendLine("The 'DequeueTimeout' connection string option has been removed. Consult the documentation for further information.");
            }

            if (dictionary.ContainsKey("maxwaittimeforconfirms"))
            {
                invalidOptionsMessage.AppendLine("The 'MaxWaitTimeForConfirms' connection string option has been removed. Consult the documentation for further information.");
            }

            if (dictionary.ContainsKey("prefetchcount"))
            {
                invalidOptionsMessage.AppendLine("The 'PrefetchCount' connection string option has been removed. Use 'EndpointConfiguration.UseTransport<RabbitMQTransport>().PrefetchCount' instead.");
            }

            if (dictionary.ContainsKey("usepublisherconfirms"))
            {
                invalidOptionsMessage.AppendLine("The 'UsePublisherConfirms' connection string option has been removed. Consult the documentation for further information.");
            }

            if (dictionary.ContainsKey("certPath"))
            {
                invalidOptionsMessage.AppendLine("The 'certPath' connection string option has been removed. Use 'EndpointConfiguration.UseTransport<RabbitMQTransport>().SetClientCertificate' instead.");
            }

            if (dictionary.ContainsKey("certPassphrase"))
            {
                invalidOptionsMessage.AppendLine("The 'certPassphrase' connection string option has been removed. Use 'EndpointConfiguration.UseTransport<RabbitMQTransport>().SetClientCertificate' instead.");
            }

            if (dictionary.ContainsKey("requestedHeartbeat"))
            {
                invalidOptionsMessage.AppendLine("The 'requestedHeartbeat' connection string option has been removed. Use 'EndpointConfiguration.UseTransport<RabbitMQTransport>().SetHeartbeatInterval' instead.");
            }

            if (dictionary.ContainsKey("retryDelay"))
            {
                invalidOptionsMessage.AppendLine("The 'retryDelay' connection string option has been removed. Use 'EndpointConfiguration.UseTransport<RabbitMQTransport>().SetNetworkRecoveryInterval' instead.");
            }
        }

        static string GetValue(Dictionary<string, string> dictionary, string key, string defaultValue)
        {
            return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
        }

        static string UriDecode(string value)
        {
            return Uri.UnescapeDataString(value);
        }

        static T GetValue<T>(Dictionary<string, string> dictionary, string key, Convert<T> convert, T defaultValue, StringBuilder invalidOptionsMessage)
        {
            if (dictionary.TryGetValue(key, out var value))
            {
                if (!convert(value, out defaultValue))
                {
                    invalidOptionsMessage.AppendLine($"'{value}' is not a valid {typeof(T).Name} value for the '{key}' connection string option.");
                }
            }

            return defaultValue;
        }

        delegate bool Convert<T>(string input, out T output);
    }
}