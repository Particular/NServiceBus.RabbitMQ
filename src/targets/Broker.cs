using System;
using System.Data.Common;
using System.Net;
using System.Text;

class Broker
{
    public static void DeleteVirtualHost()
    {
        try
        {
            GetBroker().CreateVirtualHostRequest("DELETE").GetResponse().Dispose();
        }
        catch (WebException ex) when ((ex?.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.NotFound)
        {
        }
    }

    public static void CreateVirtualHost() => GetBroker().CreateVirtualHostRequest("PUT").GetResponse().Dispose();

    public static void AddUserToVirtualHost() => GetBroker().CreateUserPermissionRequest("PUT").GetResponse().Dispose();

    public static Broker GetBroker()
    {
        var connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new Exception("The 'RabbitMQTransport_ConnectionString' environment variable is not set.");
        }

        var connectionStringParser = new RabbitMqConnectionStringParser(connectionString);

        string hostName = connectionStringParser.HostName;
        string username = connectionStringParser.UserName ?? "guest";
        string password = connectionStringParser.Password ?? "guest";
        string virtualHost = connectionStringParser.VirtualHost ?? "/";

        if (string.IsNullOrWhiteSpace(hostName))
        {
            throw new Exception("The connection string doesn't contain a value for 'host'.");
        }

        return new Broker
        {
            UserName = username,
            Password = password,
            VirtualHost = virtualHost,
            HostName = hostName,
            Port = 15672,
        };
    }

    public HttpWebRequest CreateVirtualHostRequest(string method) =>
        CreateHttpWebRequest($"http://{this.HostName}:{this.Port}/api/vhosts/{Uri.EscapeDataString(this.VirtualHost)}", method);

    public HttpWebRequest CreateUserPermissionRequest(string method)
    {
        var uriString = $"http://{this.HostName}:{this.Port}/api/permissions/{Uri.EscapeDataString(this.VirtualHost)}/{Uri.EscapeDataString(this.UserName)}";

        var request = CreateHttpWebRequest(uriString, method);

        var bodyString =
    @"{
    ""scope""       : ""client"",
    ""configure""   : "".*"",
    ""write""       : "".*"",
    ""read""        : "".*""
}";

        var bodyBytes = new ASCIIEncoding().GetBytes(bodyString);

        request.ContentLength = bodyBytes.Length;

        using (var stream = request.GetRequestStream())
        {
            stream.Write(bodyBytes, 0, bodyBytes.Length);
        }

        return request;
    }

    public HttpWebRequest CreateHttpWebRequest(string uriString, string method)
    {
        var request = WebRequest.CreateHttp(uriString);

        var encoded = Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(this.UserName + ":" + this.Password));
        request.Headers.Add("Authorization", "Basic " + encoded);
        request.ContentType = "application/json";
        request.Method = method;

        return request;
    }
    public string HostName { get; set; }

    public int Port { get; set; }

    public string VirtualHost { get; set; }

    public string UserName { get; set; }

    public string Password { get; set; }
}
