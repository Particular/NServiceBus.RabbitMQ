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
    var connectionStringBuilder = new DbConnectionStringBuilder
    {
        ConnectionString = Environment.GetEnvironmentVariable("RabbitMQTransport.ConnectionString")
    };

    ApplyDefault(connectionStringBuilder, "username", "guest");
    ApplyDefault(connectionStringBuilder, "password", "guest");
    ApplyDefault(connectionStringBuilder, "virtualhost", "nsb-rabbitmq-test");
    ApplyDefault(connectionStringBuilder, "host", "localhost");

    return new Broker
    {
        UserName = connectionStringBuilder["username"].ToString(),
        Password = connectionStringBuilder["password"].ToString(),
        VirtualHost = connectionStringBuilder["virtualhost"].ToString(),
        HostName = connectionStringBuilder["host"].ToString(),
        Port = 15672,
    };
}

public void ApplyDefault(DbConnectionStringBuilder builder, string key, string value)
{
    if (!builder.ContainsKey(key))
    {
        builder.Add(key, value);
    }
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
