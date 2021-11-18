using System;
using System.Net;
using System.Net.Http;
using System.Text;

class Broker
{
    public static void DeleteVirtualHost()
    {
        try
        {
            Send(GetBroker().CreateVirtualHostRequest(HttpMethod.Delete));
        }
        catch (HttpRequestException ex) when (ex?.StatusCode == HttpStatusCode.NotFound)
        {
        }
    }

    public static void CreateVirtualHost() => Send(GetBroker().CreateVirtualHostRequest(HttpMethod.Put));

    public static void AddUserToVirtualHost() => Send(GetBroker().CreateUserPermissionRequest(HttpMethod.Put));

    static void Send(HttpRequestMessage request)
    {
        using (var httpClient = new HttpClient())
        {
            httpClient.Send(request);
        }
    }

    public static Broker GetBroker()
    {
        var connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString") ?? "host=localhost";

        var connectionStringParser = new RabbitMqConnectionStringParser(connectionString);

        string hostName = connectionStringParser.HostName;
        string username = connectionStringParser.UserName ?? "guest";
        string password = connectionStringParser.Password ?? "guest";
        string virtualHost = connectionStringParser.VirtualHost ?? "/";
        int port = connectionStringParser.IsTls ? 443 : 15672;

        return new Broker
        {
            UserName = username,
            Password = password,
            VirtualHost = virtualHost,
            HostName = hostName,
            Port = port,
        };
    }

    public HttpRequestMessage CreateVirtualHostRequest(HttpMethod method) =>
        CreateHttpWebRequest($"http{(Port == 443 ? "s" : string.Empty)}://{HostName}:{Port}/api/vhosts/{Uri.EscapeDataString(VirtualHost)}", method);

    public HttpRequestMessage CreateUserPermissionRequest(HttpMethod method)
    {
        var uriString = $"http{(Port == 443 ? "s" : string.Empty)}://{HostName}:{Port}/api/permissions/{Uri.EscapeDataString(VirtualHost)}/{Uri.EscapeDataString(UserName)}";

        var request = CreateHttpWebRequest(uriString, method);

        var bodyString =
    @"{
    ""scope""       : ""client"",
    ""configure""   : "".*"",
    ""write""       : "".*"",
    ""read""        : "".*""
}";

        var bodyBytes = new ASCIIEncoding().GetBytes(bodyString);

        request.Content = new ByteArrayContent(bodyBytes);

        return request;
    }

    public HttpRequestMessage CreateHttpWebRequest(string uriString, HttpMethod method)
    {
        var request = new HttpRequestMessage(method, uriString);

        var encoded = Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(UserName + ":" + Password));
        request.Headers.Add("Authorization", "Basic " + encoded);
        request.Headers.Add("Content-Type", "application/json");

        return request;
    }
    public string HostName { get; set; }

    public int Port { get; set; }

    public string VirtualHost { get; set; }

    public string UserName { get; set; }

    public string Password { get; set; }
}
