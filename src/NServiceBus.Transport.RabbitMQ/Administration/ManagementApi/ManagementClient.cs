#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

class ManagementClient : IDisposable
{
    readonly HttpClient httpClient;
    readonly string escapedVirtualHost;

    const int defaultManagementPort = 15672;
    const int defaultManagementTlsPort = 15671;

    bool disposed;

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

        var handler = new SocketsHttpHandler
        {
            Credentials = new NetworkCredential(uriBuilder.UserName, uriBuilder.Password),
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            PreAuthenticate = true
        };

        httpClient = new HttpClient(handler) { BaseAddress = uriBuilder.Uri };
        var authToken = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{connectionConfiguration.UserName}:{connectionConfiguration.Password}"));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
    }

    public async Task CreatePolicy(string name, Policy policy, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(policy);

        var escapedPolicyName = Uri.EscapeDataString(name);
        using var response = await httpClient.PutAsJsonAsync($"api/policies/{escapedVirtualHost}/{escapedPolicyName}", policy, cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }

    // For ServiceControl licensing component
    public async Task<(HttpStatusCode StatusCode, string Reason, List<Binding>? Value)> GetBindingsForExchange(string exchangeName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeName);

        var escapedExchangeName = Uri.EscapeDataString(exchangeName);
        var response = await Get<List<Binding>>($"/api/exchanges/{escapedVirtualHost}/{escapedExchangeName}/bindings/destination", cancellationToken).ConfigureAwait(false);

        return response;
    }

    // For ServiceControl licensing component
    public async Task<(HttpStatusCode StatusCode, string Reason, List<Binding>? Value)> GetBindingsForQueue(string queueName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        var escapedQueueName = Uri.EscapeDataString(queueName);
        var response = await Get<List<Binding>>($"/api/queues/{escapedVirtualHost}/{escapedQueueName}/bindings", cancellationToken).ConfigureAwait(false);

        return response;
    }

    public async Task<(HttpStatusCode StatusCode, string Reason, List<FeatureFlag>? Value)> GetFeatureFlags(CancellationToken cancellationToken = default)
    {
        var response = await Get<List<FeatureFlag>>("api/feature-flags", cancellationToken).ConfigureAwait(false);

        return response;
    }

    public async Task<(HttpStatusCode StatusCode, string Reason, Overview? Value)> GetOverview(CancellationToken cancellationToken = default)
    {
        var response = await Get<Overview>("api/overview", cancellationToken).ConfigureAwait(false);

        return response;
    }

    public async Task<(HttpStatusCode StatusCode, string Reason, Queue? Value)> GetQueue(string queueName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        var escapedQueueName = Uri.EscapeDataString(queueName);
        var response = await Get<Queue>($"api/queues/{escapedVirtualHost}/{escapedQueueName}", cancellationToken).ConfigureAwait(false);

        return response;
    }

    // For ServiceControl licensing component
    public async Task<(HttpStatusCode StatusCode, string Reason, List<Queue>? Value, bool MorePages)> GetQueues(int page, int pageSize = 500, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 500);

        var (statusCode, reason, value) = await Get<GetQueuesResult>($"/api/queues/{escapedVirtualHost}/?page={page}&page_size={pageSize}", cancellationToken).ConfigureAwait(false);

        return (statusCode, reason, value?.Items, value?.Page < value?.PageCount);
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

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                httpClient.Dispose();
            }

            disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
