#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Transport.RabbitMQ.ManagementApi.Models;

class ManagementClient : IManagementApi
{
    readonly HttpClient httpClient;
    readonly string virtualHost;
    readonly string escapedVirtualHost;

    public ManagementClient(ConnectionConfiguration connectionConfiguration)
    {
        virtualHost = connectionConfiguration.VirtualHost;
        escapedVirtualHost = Uri.EscapeDataString(virtualHost);

        var uriBuilder = new UriBuilder
        {
            Scheme = connectionConfiguration.UseTls ? "https" : "http",
            Host = connectionConfiguration.Host,
            Port = connectionConfiguration.Port
        };

        httpClient = new HttpClient { BaseAddress = uriBuilder.Uri };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{connectionConfiguration.UserName}:{connectionConfiguration.Password}")));
    }

    public async Task<Queue?> GetQueue(string queueName, CancellationToken cancellationToken = default)
    {
        var escapedQueueName = Uri.EscapeDataString(queueName);
        var response = await httpClient.GetAsync($"api/queues/{escapedVirtualHost}/{escapedQueueName}", cancellationToken)
            .ConfigureAwait(false);

        return await response.EnsureSuccessStatusCode()
            .Content.ReadFromJsonAsync<Queue>(cancellationToken)
            .ConfigureAwait(false);
    }
}
