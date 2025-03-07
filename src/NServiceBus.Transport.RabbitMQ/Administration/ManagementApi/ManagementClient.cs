﻿#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

class ManagementClient : IDisposable
{
    readonly HttpClient httpClient;
    readonly string escapedVirtualHost;

    const int defaultManagementPort = 15672;
    const int defaultManagementTlsPort = 15671;

    const string nullErrorMessage = "RabbitMQ management API returned success but deserializing the response body returned null.";

    bool disposed;

    public ManagementClient(ConnectionConfiguration connectionConfiguration, ManagementApiConfiguration? managementApiConfiguration = null)
    {
        ArgumentNullException.ThrowIfNull(connectionConfiguration);

        UriBuilder uriBuilder;

        if (managementApiConfiguration?.Url is not null)
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
                UserName = managementApiConfiguration?.UserName ?? connectionConfiguration.UserName,
                Password = managementApiConfiguration?.Password ?? connectionConfiguration.Password
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
    public async Task<List<Binding>> GetBindingsForExchange(string exchangeName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeName);

        var escapedExchangeName = Uri.EscapeDataString(exchangeName);
        var response = await httpClient.GetFromJsonAsync<List<Binding>>($"/api/exchanges/{escapedVirtualHost}/{escapedExchangeName}/bindings/destination", cancellationToken).ConfigureAwait(false);

        return response ?? throw new HttpRequestException(nullErrorMessage);
    }

    // For ServiceControl licensing component
    public async Task<List<Binding>> GetBindingsForQueue(string queueName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        var escapedQueueName = Uri.EscapeDataString(queueName);
        var response = await httpClient.GetFromJsonAsync<List<Binding>>($"/api/queues/{escapedVirtualHost}/{escapedQueueName}/bindings", cancellationToken).ConfigureAwait(false);

        return response ?? throw new HttpRequestException(nullErrorMessage);
    }

    public async Task<List<FeatureFlag>> GetFeatureFlags(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetFromJsonAsync<List<FeatureFlag>>("api/feature-flags", cancellationToken).ConfigureAwait(false);

        return response ?? throw new HttpRequestException(nullErrorMessage);
    }

    public async Task<Overview> GetOverview(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetFromJsonAsync<Overview>("api/overview", cancellationToken).ConfigureAwait(false);

        return response ?? throw new HttpRequestException(nullErrorMessage);
    }

    public async Task<Queue> GetQueue(string queueName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        var escapedQueueName = Uri.EscapeDataString(queueName);
        var response = await httpClient.GetFromJsonAsync<Queue>($"api/queues/{escapedVirtualHost}/{escapedQueueName}", cancellationToken).ConfigureAwait(false);

        return response ?? throw new HttpRequestException(nullErrorMessage);
    }

    // For ServiceControl licensing component
    public async Task<(List<Queue> Queues, bool MorePages)> GetQueues(int page, int pageSize = 500, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 500);

        var response = await httpClient.GetFromJsonAsync<GetQueuesResult>($"/api/queues/{escapedVirtualHost}/?page={page}&page_size={pageSize}", cancellationToken).ConfigureAwait(false);

        if (response is null)
        {
            throw new HttpRequestException(nullErrorMessage);
        }

        return (response.Items, response.Page < response.PageCount);
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
