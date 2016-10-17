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
using NServiceBus.AcceptanceTests.ScenarioDescriptors;
using NServiceBus.Transport.RabbitMQ.AcceptanceTests;
using RabbitMQ.Client;

class ConfigureScenariosForRabbitMQTransport : IConfigureSupportedScenariosForTestExecution
{
    public IEnumerable<Type> UnsupportedScenarioDescriptorTypes => new[] { typeof(AllDtcTransports), typeof(AllNativeMultiQueueTransactionTransports), typeof(AllTransportsWithMessageDrivenPubSub), typeof(AllTransportsWithoutNativeDeferralAndWithAtomicSendAndReceive) };
}

class ConfigureEndpointRabbitMQTransport : IConfigureEndpointTestExecution
{
    string connectionString;

    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings)
    {
        connectionString = settings.Get<string>("Transport.ConnectionString");
        configuration.UseTransport<RabbitMQTransport>().ConnectionString(connectionString).UseAutomaticRoutingTopology();

        return TaskEx.CompletedTask;
    }

    public Task Cleanup() => PurgeQueues();

    async Task PurgeQueues()
    {
        var connectionFactory = CreateConnectionFactory();

        var queues = await GetQueues(connectionFactory);

        using (var connection = connectionFactory.CreateConnection())
        using (var channel = connection.CreateModel())
        {
            foreach (var queue in queues)
            {
                try
                {
                    channel.QueuePurge(queue.Name);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Unable to clear queue {0}: {1}", queue.Name, ex);
                }
            }
        }
    }

    ConnectionFactory CreateConnectionFactory()
    {
        var match = Regex.Match(connectionString, string.Format("[^\\w]*{0}=(?<{0}>[^;]+)", "host"), RegexOptions.IgnoreCase);

        var username = match.Groups["UserName"].Success ? match.Groups["UserName"].Value : "guest";
        var password = match.Groups["Password"].Success ? match.Groups["Password"].Value : "guest";
        var host = match.Groups["host"].Success ? match.Groups["host"].Value : "localhost";
        var virtualHost = match.Groups["VirtualHost"].Success ? match.Groups["VirtualHost"].Value : "/";

        var connectionFactory = new ConnectionFactory
        {
            UserName = username,
            Password = password,
            VirtualHost = virtualHost,
            HostName = host,
            AutomaticRecoveryEnabled = true
        };

        connectionFactory.ClientProperties["purpose"] = "Test Queue Purger";

        return connectionFactory;
    }

    // Requires that the RabbitMQ Management API has been enabled: https://www.rabbitmq.com/management.html
    async Task<IEnumerable<Queue>> GetQueues(ConnectionFactory connectionFactory)
    {
        var httpClient = CreateHttpClient(connectionFactory);

        var queueResult = await httpClient.GetAsync(string.Format(CultureInfo.InvariantCulture, "api/queues/{0}", Uri.EscapeDataString(connectionFactory.VirtualHost)));
        queueResult.EnsureSuccessStatusCode();

        var content = await queueResult.Content.ReadAsStringAsync();

        return JsonConvert.DeserializeObject<List<Queue>>(content);
    }

    HttpClient CreateHttpClient(ConnectionFactory details)
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

    class Queue
    {
        public string Name { get; set; }
    }
}