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
