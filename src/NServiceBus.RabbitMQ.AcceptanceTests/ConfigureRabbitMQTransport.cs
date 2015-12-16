using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using RabbitMQ.Client;

internal class ConfigureRabbitMQTransport : IConfigureTestExecution
{
    private string connectionString;

    public Task Configure(BusConfiguration configuration, IDictionary<string, string> settings)
    {
        connectionString = settings["Transport.ConnectionString"];
        configuration.UseTransport<RabbitMQTransport>().ConnectionString(connectionString);
        return Task.FromResult(0);
    }

    public Task Cleanup()
    {
        PurgeQueues();
        return Task.FromResult(0);
    }

    private void PurgeQueues()
    {
        var connectionFactory = CreateConnectionFactory(connectionString);

        var queues = GetQueues(connectionFactory);

        var connection = connectionFactory.CreateConnection();
        using (var model = connection.CreateModel())
        {
            connection.AutoClose = true;
            foreach (var queue in queues)
            {
                try
                {
                    var cleared = model.QueuePurge(queue.Name);
                    Console.WriteLine("Cleared {0} message(s) out of {1}", cleared, queue.Name);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Unable to clear queue {0}: {1}", queue.Name, ex);
                }
            }
        }
    }

    private ConnectionFactory CreateConnectionFactory(string connectionString)
    {
        var match = Regex.Match(connectionString, string.Format("[^\\w]*{0}=(?<{0}>[^;]+)", "host"), RegexOptions.IgnoreCase);

        var username = match.Groups["UserName"].Success ? match.Groups["UserName"].Value : "guest";
        var password = match.Groups["Password"].Success ? match.Groups["Password"].Value : "guest";
        var host = match.Groups["host"].Success ? match.Groups["host"].Value : "localhost";
        var virtualHost = match.Groups["VirtualHost"].Success ? match.Groups["VirtualHost"].Value : "/";

        return new ConnectionFactory
        {
            UserName = username,
            Password = password,
            VirtualHost = virtualHost,
            HostName = host,
            AutomaticRecoveryEnabled = true
        };
    }

    // Requires that the RabbitMQ Management API has been enabled: https://www.rabbitmq.com/management.html
    private IEnumerable<Queue> GetQueues(ConnectionFactory connectionFactory)
    {
        var httpClient = CreateHttpClient(connectionFactory);
        var queueResult = httpClient.GetAsync(string.Format(CultureInfo.InvariantCulture, "api/queues/{0}", Uri.EscapeDataString(connectionFactory.VirtualHost))).Result;
        queueResult.EnsureSuccessStatusCode();
        return JsonConvert.DeserializeObject<List<Queue>>(queueResult.Content.ReadAsStringAsync().Result);
    }

    private HttpClient CreateHttpClient(ConnectionFactory details)
    {
        var handler = new HttpClientHandler
        {
            Credentials = new NetworkCredential(details.UserName, details.Password)
        };
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(string.Format(CultureInfo.InvariantCulture, "http://{0}:15672/", details.HostName))
        };
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return httpClient;
    }

    private class Queue
    {
        public string Name { get; set; }
    }
}