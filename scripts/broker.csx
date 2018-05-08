#r "System.Data"

using System.Data.Common;
using System.Net;

public void DeleteVirtualHost()
{
    try
    {
        CreateVirtualHostRequest("DELETE", GetBroker()).GetResponse().Dispose();
    }
    catch (WebException ex) when ((ex?.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.NotFound)
    {
    }
}

public void CreateVirtualHost() => CreateVirtualHostRequest("PUT", GetBroker()).GetResponse().Dispose();

public void AddUserToVirtualHost() => CreateUserPermissionRequest("PUT", GetBroker()).GetResponse().Dispose();

public Broker GetBroker()
{
    var connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString");

    if (string.IsNullOrEmpty(connectionString))
    {
        throw new Exception("The 'RabbitMQTransport_ConnectionString' environment variable is not set.");
    }

    var connectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = connectionString };

    string hostName;
    
    object value;
    if (connectionStringBuilder.TryGetValue("host", out value))
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

public static string GetOrDefault(this DbConnectionStringBuilder builder, string key, string defaultValue)
{
    object value;
    return builder.TryGetValue(key, out value) ? value.ToString() : defaultValue;
}

public HttpWebRequest CreateVirtualHostRequest(string method, Broker broker) =>
    CreateHttpWebRequest(
        $"http://{broker.HostName}:{broker.Port}/api/vhosts/{Uri.EscapeDataString(broker.VirtualHost)}",
        method,
        broker);

public HttpWebRequest CreateUserPermissionRequest(string method, Broker broker)
{
    var uriString = $"http://{broker.HostName}:{broker.Port}/api/permissions/{Uri.EscapeDataString(broker.VirtualHost)}/{Uri.EscapeDataString(broker.UserName)}";

    var request = CreateHttpWebRequest(uriString, method, broker);

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

public HttpWebRequest CreateHttpWebRequest(string uriString, string method, Broker broker)
{
    var request = HttpWebRequest.CreateHttp(uriString);

    var encoded = Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(broker.UserName + ":" + broker.Password));
    request.Headers.Add("Authorization", "Basic " + encoded);
    request.ContentType = "application/json";
    request.Method = method;

    return request;
}

public class Broker
{
    public string HostName { get; set; }
    
    public int Port { get; set; }
    
    public string VirtualHost { get; set; }
    
    public string UserName { get; set; }
    
    public string Password { get; set; }
}
