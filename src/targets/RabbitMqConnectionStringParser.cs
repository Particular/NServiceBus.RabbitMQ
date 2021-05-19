using System;
using System.Data.Common;

class RabbitMqConnectionStringParser
{
    public RabbitMqConnectionStringParser(string connectionString)
    {
        if (connectionString.StartsWith("amqp"))
        {
            ParseAmqpConnectionString(connectionString);
        }
        else
        {
            ParseRabbitConnectionString(connectionString);
        }
    }

    void ParseRabbitConnectionString(string connectionString)
    {
        var connectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = connectionString };

        if (connectionStringBuilder.TryGetValue("username", out var value))
        {
            UserName = value.ToString();
        }
        else if (connectionStringBuilder.TryGetValue("user", out var userValue))
        {
            UserName = userValue.ToString();
        }

        if (connectionStringBuilder.TryGetValue("password", out value))
        {
            Password = value.ToString();
        }

        if (connectionStringBuilder.TryGetValue("virtualhost", out value))
        {
            VirtualHost = value.ToString();
        }

        if (connectionStringBuilder.TryGetValue("host", out value))
        {
            HostName = value.ToString();
        }
        else
        {
            throw new Exception("The connection string doesn't contain a value for 'host'.");
        }
    }

    void ParseAmqpConnectionString(string connectionString)
    {
        var uri = new Uri(connectionString);

        HostName = uri.Host;

        if (!uri.IsDefaultPort)
        {
            Port = uri.Port;
        }

        IsTls = uri.Scheme == "amqps";

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var userPass = uri.UserInfo.Split(':');
            if (userPass.Length > 2)
            {
                throw new Exception($"Invalid user information: {uri.UserInfo}. Expected user and password separated by a colon.");
            }

            UserName = Uri.UnescapeDataString(userPass[0]);
            if (userPass.Length == 2)
            {
                Password = Uri.UnescapeDataString(userPass[1]);
            }
        }

        if (uri.Segments.Length > 2)
        {
            throw new Exception($"Multiple segments are not allowed in the path of an AMQP URI: {string.Join(", ", uri.Segments)}");
        }

        if (uri.Segments.Length == 2)
        {
            VirtualHost = Uri.UnescapeDataString(uri.Segments[1]);
        }
    }

    public string UserName { get; private set; }
    public string Password { get; private set; }
    public string VirtualHost { get; private set; }
    public string HostName { get; private set; }
    public bool IsTls { get; private set; }
    public int? Port { get; private set; }
}