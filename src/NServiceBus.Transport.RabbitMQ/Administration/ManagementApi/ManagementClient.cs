#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;

using System;
using System.Collections.Generic;
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

    public async Task<Response<Queue?>> GetQueue(string queueName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        var escapedQueueName = Uri.EscapeDataString(queueName);
        var response = await httpClient.GetAsync($"api/queues/{escapedVirtualHost}/{escapedQueueName}", cancellationToken).ConfigureAwait(false);
        var content = await GetResponseContent<Queue>(response, cancellationToken).ConfigureAwait(false);

        return new Response<Queue?>(
            response.StatusCode,
            response.ReasonPhrase ?? string.Empty,
            content);
    }

    public async Task<Response<Overview?>> GetOverview(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"api/overview", cancellationToken).ConfigureAwait(false);
        var content = await GetResponseContent<Overview>(response, cancellationToken).ConfigureAwait(false);

        return new Response<Overview?>(
            response.StatusCode,
            response.ReasonPhrase ?? string.Empty,
            content);
    }

    public async Task<Response<List<FeatureFlag>?>> GetFeatureFlags(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"api/feature-flags", cancellationToken).ConfigureAwait(false);
        var content = await GetResponseContent<List<FeatureFlag>>(response, cancellationToken).ConfigureAwait(false);

        return new Response<List<FeatureFlag>?>(
            response.StatusCode,
            response.ReasonPhrase ?? string.Empty,
            content);
    }

    public async Task CreatePolicy(string name, Policy policy, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(policy);

        var escapedPolicyName = Uri.EscapeDataString(name);
        var response = await httpClient.PutAsJsonAsync($"api/policies/{escapedVirtualHost}/{escapedPolicyName}", policy, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }

    // TODO: Update comment - This is used for the throughput component in ServiceControl
    public async Task<Response<Pagination?>> GetPage(int page, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/queues/{escapedVirtualHost}/?page={page}&page_size=500&name=&use_regex=false&pagination=true", cancellationToken).ConfigureAwait(false);
        var content = await GetResponseContent<Pagination>(response, cancellationToken).ConfigureAwait(false);

        return new Response<Pagination?>(
            response.StatusCode,
            response.ReasonPhrase ?? string.Empty,
            content);
    }

    // TODO: Update comment - This is used for the throughput component in ServiceControl
    public async Task<Response<List<Binding?>>> GetQueueBindings(string queueName, CancellationToken cancellationToken = default)
    {
        var escapedQueueName = Uri.EscapeDataString(queueName);
        var response = await httpClient.GetAsync($"/api/queues/{escapedVirtualHost}/{escapedQueueName}/bindings", cancellationToken).ConfigureAwait(false);
        var content = await GetResponseContent<List<Binding?>>(response, cancellationToken).ConfigureAwait(false) ?? [];

        return new Response<List<Binding?>>(
            response.StatusCode,
            response.ReasonPhrase ?? string.Empty,
            content);
    }

    // TODO: Update comment - This is used for the throughput component in ServiceControl
    public async Task<Response<List<Binding?>>> GetExchangeBindingsDestination(string queueName, CancellationToken cancellationToken = default)
    {
        var escapedQueueName = Uri.EscapeDataString(queueName);
        var response = await httpClient.GetAsync($"/api/queues/{escapedVirtualHost}/{escapedQueueName}/bindings", cancellationToken).ConfigureAwait(false);
        var content = await GetResponseContent<List<Binding?>>(response, cancellationToken).ConfigureAwait(false) ?? [];

        return new Response<List<Binding?>>(
            response.StatusCode,
            response.ReasonPhrase ?? string.Empty,
            content);
    }


    static async Task<T?> GetResponseContent<T>(HttpResponseMessage response, CancellationToken cancellationToken) where T : class
    {
        if (response.IsSuccessStatusCode && response.Content is not null)
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
        }

        return default;
    }
}
