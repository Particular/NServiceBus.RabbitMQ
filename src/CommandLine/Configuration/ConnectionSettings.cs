namespace NServiceBus.Transport.RabbitMQ.CommandLine.Configuration
{
    using System.Data.Common;
    using System.Security.Cryptography.X509Certificates;

    /// <summary>
    /// This code was copied from the NServiceBus.Transport.RabbitmQ AmqpConnectionString.cs and NServiceBusConnectionString.cs
    /// </summary>
    class ConnectionSettings
    {
        TimeSpan heartbeatInterval = TimeSpan.FromMinutes(1);
        TimeSpan networkRecoveryInterval = TimeSpan.FromSeconds(10);
        int? port = null;

        ConnectionSettings()
        {
        }

        public string? Host { get; private set; }

        public int Port
        {
            get
            {
                return port ?? DefaultPort;
            }

            private set
            {
                port = value;
            }
        }

        public string VHost { get; private set; } = "/";

        public bool UseTLS { get; private set; }

        public string? UserName { get; private set; }

        public string? Password { get; private set; }

        public string? ConnectionString { get; private set; }

        public TimeSpan NetworkRecoveryInterval
        {
            get => networkRecoveryInterval;
            set
            {
                Guard.AgainstNegativeAndZero("value", value);
                networkRecoveryInterval = value;
            }
        }

        public TimeSpan HeartbeatInterval
        {
            get => heartbeatInterval;
            set
            {
                Guard.AgainstNegativeAndZero("value", value);
                heartbeatInterval = value;
            }
        }

        public X509Certificate2? ClientCertificate { get; private set; }

        public X509Certificate2Collection? ClientCertificateCollection
        {
            get
            {
                if (ClientCertificate != null)
                {
                    return new X509Certificate2Collection(ClientCertificate);
                }

                return null;
            }
        }

        /// <summary>
        ///     Should the client validate the broker certificate when connecting via TLS.
        /// </summary>
        public bool ValidateRemoteCertificate { get; set; } = true;

        /// <summary>
        ///     Specifies if an external authentication mechanism should be used for client authentication.
        /// </summary>
        public bool UseExternalAuthMechanism { get; set; } = false;


        int DefaultPort => UseTLS ? 5671 : 5672;

        public static ConnectionSettings Parse(string connectionString)
        {
            if (connectionString.StartsWith("amqp", StringComparison.OrdinalIgnoreCase))
            {
                return ParseAmqpConnectionString(connectionString);
            }
            else
            {
                return ParseNServiceBusConnectionString(connectionString);
            }
        }

        static ConnectionSettings ParseAmqpConnectionString(string connectionString)
        {
            ConnectionSettings connectionSettings = new();

            connectionSettings.ConnectionString = connectionString;

            var uri = new Uri(connectionString);

            connectionSettings.Host = uri.Host;

            if (!uri.IsDefaultPort)
            {
                connectionSettings.Port = uri.Port;
            }

            connectionSettings.UseTLS = uri.Scheme == "amqps";

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var userPass = uri.UserInfo.Split(':');
                if (userPass.Length > 2)
                {
                    throw new Exception($"Invalid user information: {uri.UserInfo}. Expected user and password separated by a colon.");
                }

                connectionSettings.UserName = UriDecode(userPass[0]);
                if (userPass.Length == 2)
                {
                    connectionSettings.Password = UriDecode(userPass[1]);
                }
            }

            if (uri.Segments.Length > 2)
            {
                throw new Exception($"Multiple segments are not allowed in the path of an AMQP URI: {string.Join(", ", uri.Segments)}");
            }

            if (uri.Segments.Length == 2)
            {
                connectionSettings.VHost = UriDecode(uri.Segments[1]);
            }

            return connectionSettings;

        }

        static ConnectionSettings ParseNServiceBusConnectionString(string connectionString)
        {
            var dictionary1 = new DbConnectionStringBuilder { ConnectionString = connectionString }
                .OfType<KeyValuePair<string, object>>()
                .ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.OrdinalIgnoreCase);

            CheckDeprecatedSettings(dictionary1);
            var dictionary = dictionary1;

            ConnectionSettings connectionSettings = new();
            connectionSettings.ConnectionString = connectionString;

            if (dictionary.TryGetValue("port", out var portString))
            {
                if (!int.TryParse(portString, out var port))
                {
                    throw new Exception($"'{portString}' is not a valid value for the 'port' connection string option.");
                }

                connectionSettings.Port = port;
            }

            if (dictionary.TryGetValue("host", out var host))
            {
                if (host != null && host.Contains(":"))
                {
                    var hostAndPort = host.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                    if (hostAndPort.Length > 2)
                    {
                        throw new Exception($"'{host}' is not a valid value for the 'host' connection string option.");
                    }

                    connectionSettings.Host = hostAndPort[0];
                    if (!int.TryParse(hostAndPort[1], out var port))
                    {
                        throw new Exception($"'{host}' is not a valid value for the 'host' connection string option.");
                    }

                    connectionSettings.Port = port;
                }
                else
                {
                    connectionSettings.Host = host;
                }
            }
            else
            {
                throw new Exception("Missing required 'host' connection string option.");
            }

            if (dictionary.TryGetValue("virtualHost", out var vhost))
            {
                if (vhost != null)
                {
                    connectionSettings.VHost = vhost;
                }

            }

            if (dictionary.TryGetValue("userName", out var userName))
            {
                connectionSettings.UserName = userName;
            }

            if (dictionary.TryGetValue("password", out var password))
            {
                connectionSettings.Password = password;
            }

            if (dictionary.TryGetValue("retryDelay", out var retryDelayString))
            {
                if (!TimeSpan.TryParse(retryDelayString, out var retryDelay))
                {
                    throw new Exception($"'{retryDelayString}' is not a valid value for the 'retryDelay' connection string option.");
                }

                connectionSettings.NetworkRecoveryInterval = retryDelay;
            }

            if (dictionary.TryGetValue("requestedHeartbeat", out var heartbeatString))
            {
                if (int.TryParse(heartbeatString, out var heartbeatSeconds))
                {
                    connectionSettings.HeartbeatInterval = TimeSpan.FromSeconds(heartbeatSeconds);
                }
                else if (TimeSpan.TryParse(heartbeatString, out var heartbeatTimeSpan))
                {
                    connectionSettings.HeartbeatInterval = heartbeatTimeSpan;
                }
                else
                {
                    throw new Exception($"'{heartbeatString}' is not a valid value for the 'requestedHeartbeat' connection string option.");
                }
            }

            if (dictionary.TryGetValue("certPath", out var certPath)
                && dictionary.TryGetValue("certPassphrase", out var passPhrase)
                && !string.IsNullOrWhiteSpace(certPath))
            {
                connectionSettings.ClientCertificate = new X509Certificate2(certPath, passPhrase);
            }

            if (dictionary.TryGetValue("useTls", out var useTlsString))
            {
                if (!bool.TryParse(useTlsString, out var useTls))
                {
                    throw new Exception($"'{useTlsString}' is not a valid value for the 'useTls' connection string option.");
                }

                connectionSettings.UseTLS = useTls;
            }

            return connectionSettings;
        }

        static void CheckDeprecatedSettings(Dictionary<string, string?> dictionary)
        {
            if (dictionary.TryGetValue("host", out var value))
            {
                var hostsAndPorts = value?.Split(',');

                if (hostsAndPorts?.Length > 1)
                {
                    throw new Exception("Multiple hosts are no longer supported. If using RabbitMQ in a cluster, use the RabbitMQClusterTransport and the .AddConnectionString method.");
                }
            }

            if (dictionary.ContainsKey("dequeuetimeout"))
            {
                throw new Exception("The 'DequeueTimeout' connection string option has been removed. Consult the documentation for further information.");
            }

            if (dictionary.ContainsKey("maxwaittimeforconfirms"))
            {
                throw new Exception("The 'MaxWaitTimeForConfirms' connection string option has been removed. Consult the documentation for further information.");
            }

            if (dictionary.ContainsKey("prefetchcount"))
            {
                throw new Exception("The 'PrefetchCount' connection string option has been removed. Use 'EndpointConfiguration.UseTransport<RabbitMQTransport>().PrefetchCount' instead.");
            }

            if (dictionary.ContainsKey("usepublisherconfirms"))
            {
                throw new Exception("The 'UsePublisherConfirms' connection string option has been removed. Consult the documentation for further information.");
            }
        }

        static string UriDecode(string value)
        {
            return Uri.UnescapeDataString(value);
        }
    }
}
