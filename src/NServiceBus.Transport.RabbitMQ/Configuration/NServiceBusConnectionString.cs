namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;

    static class NServiceBusConnectionString
    {
        public static Action<RabbitMQTransport> Parse(string connectionString)
        {
            var dictionary1 = new DbConnectionStringBuilder { ConnectionString = connectionString }
                .OfType<KeyValuePair<string, object>>()
                .ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.OrdinalIgnoreCase);

            CheckDeprecatedSettings(dictionary1);
            var dictionary = dictionary1;

            return transport =>
            {
                if (dictionary.TryGetValue("port", out var portString))
                {
                    if (!int.TryParse(portString, out var port))
                    {
                        throw new Exception($"'{portString}' is not a valid value for the 'port' connection string option.");
                    }

                    transport.Port = port;
                }

                if (dictionary.TryGetValue("host", out var host))
                {
                    if (host.Contains(":"))
                    {
                        var hostAndPort = host.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                        if (hostAndPort.Length > 2)
                        {
                            throw new Exception($"'{host}' is not a valid value for the 'host' connection string option.");
                        }

                        transport.Host = hostAndPort[0];
                        if (!int.TryParse(hostAndPort[1], out var port))
                        {
                            throw new Exception($"'{host}' is not a valid value for the 'host' connection string option.");
                        }

                        transport.Port = port;
                    }
                    else
                    {
                        transport.Host = host;
                    }
                }
                else
                {
                    throw new Exception("Missing required 'host' connection string option.");
                }

                if (dictionary.TryGetValue("virtualHost", out var vhost))
                {
                    transport.VHost = vhost;
                }

                if (dictionary.TryGetValue("userName", out var userName))
                {
                    transport.UserName = userName;
                }

                if (dictionary.TryGetValue("password", out var password))
                {
                    transport.Password = password;
                }

                if (dictionary.TryGetValue("retryDelay", out var retryDelayString))
                {
                    if (!TimeSpan.TryParse(retryDelayString, out var retryDelay))
                    {
                        throw new Exception($"'{retryDelayString}' is not a valid value for the 'retryDelay' connection string option.");
                    }

                    transport.NetworkRecoveryInterval = retryDelay;
                }

                if (dictionary.TryGetValue("requestedHeartbeat", out var heartbeatString))
                {
                    if (int.TryParse(heartbeatString, out var heartbeatSeconds))
                    {
                        transport.HeartbeatInterval = TimeSpan.FromSeconds(heartbeatSeconds);
                    }
                    else if (TimeSpan.TryParse(heartbeatString, out var heartbeatTimeSpan))
                    {
                        transport.HeartbeatInterval = heartbeatTimeSpan;
                    }
                    else
                    {
                        throw new Exception($"'{heartbeatString}' is not a valid value for the 'requestedHeartbeat' connection string option.");
                    }
                }

                if (dictionary.TryGetValue("certPath", out var certPath)
                    && dictionary.TryGetValue("certPassphrase", out var passPhrase))
                {
                    transport.ClientCertificate = new X509Certificate2(certPath, passPhrase);
                }

                if (dictionary.TryGetValue("useTls", out var useTlsString))
                {
                    if (!bool.TryParse(useTlsString, out var useTls))
                    {
                        throw new Exception($"'{useTlsString}' is not a valid value for the 'useTls' connection string option.");
                    }

                    transport.UseTLS = useTls;
                }
            };
        }


        static void CheckDeprecatedSettings(Dictionary<string, string> dictionary)
        {
            if (dictionary.TryGetValue("host", out var value))
            {
                var hostsAndPorts = value.Split(',');

                if (hostsAndPorts.Length > 1)
                {
                    throw new Exception("Multiple hosts are no longer supported. If using RabbitMQ in a cluster, consider using a load balancer to represent the nodes as a single host.");
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
    }
}
