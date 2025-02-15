#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

class ManagementClient
{
    readonly HttpClient httpClient;
    readonly string escapedVirtualHost;

    const int defaultManagementPort = 15672;
    const int defaultManagementTlsPort = 15671;

    public ManagementClient(ConnectionConfiguration connectionConfiguration, ManagementApiConfiguration? managementApiConfiguration = null)
    {
        ArgumentNullException.ThrowIfNull(connectionConfiguration);

        UriBuilder uriBuilder;

        if (managementApiConfiguration is not null)
        {
            uriBuilder = new UriBuilder(managementApiConfiguration.Url)
            {
                UserName = managementApiConfiguration.UserName ?? connectionConfiguration.UserName,
                Password = managementApiConfiguration.Password ?? connectionConfiguration.Password
            };

            if (uriBuilder.Scheme is not "http" or "https")
            {
                throw new NotSupportedException($"URL scheme '{uriBuilder.Scheme}' is not supported for the RabbitMQ management API URL. Valid schemes are 'http' and 'https'.");
            }
        }
        else
        {
            uriBuilder = new UriBuilder
            {
                Scheme = connectionConfiguration.UseTls ? "https" : "http",
                Host = connectionConfiguration.Host,
                Port = connectionConfiguration.UseTls ? defaultManagementTlsPort : defaultManagementPort,
                UserName = connectionConfiguration.UserName,
                Password = connectionConfiguration.Password
            };
        }

        escapedVirtualHost = Uri.EscapeDataString(connectionConfiguration.VirtualHost);

        httpClient = new HttpClient { BaseAddress = uriBuilder.Uri };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{uriBuilder.UserName}:{uriBuilder.Password}")));
    }

    public async Task<(HttpStatusCode StatusCode, string Reason, Queue? Value)> GetQueue(string queueName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        var escapedQueueName = Uri.EscapeDataString(queueName);
        var response = await Get<Queue>($"api/queues/{escapedVirtualHost}/{escapedQueueName}", cancellationToken).ConfigureAwait(false);

        return response;
    }

    public async Task<(HttpStatusCode StatusCode, string Reason, Overview? Value)> GetOverview(CancellationToken cancellationToken = default)
    {
        var response = await Get<Overview>("api/overview", cancellationToken).ConfigureAwait(false);

        return response;
    }

    public async Task<(HttpStatusCode StatusCode, string Reason, List<FeatureFlag>? Value)> GetFeatureFlags(CancellationToken cancellationToken = default)
    {
        var response = await Get<List<FeatureFlag>>("api/feature-flags", cancellationToken).ConfigureAwait(false);

        return response;
    }

    public async Task CreatePolicy(string name, Policy policy, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(policy);

        var escapedPolicyName = Uri.EscapeDataString(name);
        using var response = await httpClient.PutAsJsonAsync($"api/policies/{escapedVirtualHost}/{escapedPolicyName}", policy, cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }

    // TODO: Update comment - This is used for the throughput component in ServiceControl
    public async Task<(HttpStatusCode StatusCode, string Reason, Pagination? Value)> GetVhostQueuesPage(int page, CancellationToken cancellationToken = default)
    {
        var response = await Get<Pagination>($"/api/queues/{escapedVirtualHost}/?page={page}&page_size=500&name=&use_regex=false&pagination=true", cancellationToken).ConfigureAwait(false);
        return response;
    }

    // TODO: Update comment - This is used for the throughput component in ServiceControl
    public async Task<(HttpStatusCode StatusCode, string Reason, List<Binding>? Value)> GetQueueBindings(string queueName, CancellationToken cancellationToken = default)
    {
        var escapedQueueName = Uri.EscapeDataString(queueName);
        var response = await Get<List<Binding>>($"/api/queues/{escapedVirtualHost}/{escapedQueueName}/bindings", cancellationToken).ConfigureAwait(false);
        return response;
    }

    // TODO: Update comment - This is used for the throughput component in ServiceControl
    public async Task<(HttpStatusCode StatusCode, string Reason, List<Binding>? Value)> GetExchangeDestinationBindings(string queueName, CancellationToken cancellationToken = default)
    {
        var escapedQueueName = Uri.EscapeDataString(queueName);
        var response = await Get<List<Binding>>($"/api/exchanges/{escapedVirtualHost}/{escapedQueueName}/bindings/destination", cancellationToken).ConfigureAwait(false);
        return response;
    }

    public async Task<(HttpStatusCode StatusCode, string Reason, List<Queue>?)> GetVhostQueues(CancellationToken cancellationToken = default)
    {
        var response = await Get<List<Queue>>($"/api/queues/{escapedVirtualHost}", cancellationToken).ConfigureAwait(false);
        return response;
    }

    async Task<(HttpStatusCode StatusCode, string Reason, T? Value)> Get<T>(string url, CancellationToken cancellationToken)
    {
        T? value = default;

        using var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            value = await response.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
        }

        return (response.StatusCode, response.ReasonPhrase ?? string.Empty, value);
    }
}
