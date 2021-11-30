using System;
using System.Data.Common;
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
        var connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new Exception("The 'RabbitMQTransport_ConnectionString' environment variable is not set.");
        }

        var connectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = connectionString };

        string hostName;

        if (connectionStringBuilder.TryGetValue("host", out var value))
        {
            hostName = value.ToString();
        }
        else
        {
            throw new Exception("The connection string doesn't contain a value for 'host'.");
        }

        return new Broker
        {
            UserName = connectionStringBuilder.GetOrDefault("username", "guest"),
            Password = connectionStringBuilder.GetOrDefault("password", "guest"),
            VirtualHost = connectionStringBuilder.GetOrDefault("virtualhost", "/"),
            HostName = hostName,
            Port = 15672,
        };
    }

    public HttpRequestMessage CreateVirtualHostRequest(HttpMethod method) =>
        CreateHttpWebRequest($"http://{HostName}:{Port}/api/vhosts/{Uri.EscapeDataString(VirtualHost)}", method);

    public HttpRequestMessage CreateUserPermissionRequest(HttpMethod method)
    {
        var uriString = $"http://{HostName}:{Port}/api/permissions/{Uri.EscapeDataString(VirtualHost)}/{Uri.EscapeDataString(UserName)}";

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
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        return request;
    }

    public HttpRequestMessage CreateHttpWebRequest(string uriString, HttpMethod method)
    {
        var request = new HttpRequestMessage(method, uriString);

        var encoded = Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(UserName + ":" + Password));
        request.Headers.Add("Authorization", "Basic " + encoded);

        return request;
    }
    public string HostName { get; set; }

    public int Port { get; set; }

    public string VirtualHost { get; set; }

    public string UserName { get; set; }

    public string Password { get; set; }
}
