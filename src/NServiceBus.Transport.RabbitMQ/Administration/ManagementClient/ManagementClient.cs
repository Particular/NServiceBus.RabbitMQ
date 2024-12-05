#nullable enable

namespace NServiceBus.Transport.RabbitMQ.Administration.ManagementClient;

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Transport.RabbitMQ.Administration.ManagementClient.Models;

class ManagementClient : IManagementClient
{
    readonly HttpClient httpClient;
    readonly string virtualHost;
    readonly string escapedVirtualHost;

    public ManagementClient(ConnectionConfiguration connectionConfiguration, X509Certificate2Collection? managementCertCollection = null)
    {
        if (connectionConfiguration == null)
        {
            throw new ArgumentNullException(nameof(connectionConfiguration));
        }

        virtualHost = connectionConfiguration.VirtualHost;
        escapedVirtualHost = Uri.EscapeDataString(virtualHost);

        var handler = new HttpClientHandler();
        if (connectionConfiguration.UseTls)
        {
            ConfigureSsl(handler, managementCertCollection);
        }

        var uriBuilder = new UriBuilder
        {
            Scheme = connectionConfiguration.UseTls ? "https" : "http",
            Host = connectionConfiguration.Host,
            Port = connectionConfiguration.Port,
        };

        httpClient = new HttpClient(handler) { BaseAddress = uriBuilder.Uri };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{connectionConfiguration.UserName}:{connectionConfiguration.Password}")));
    }

    void ConfigureSsl(HttpClientHandler handler, X509Certificate2Collection? managementCertCollection)
    {
        if (managementCertCollection != null)
        {
            handler.ClientCertificates.AddRange(managementCertCollection);
        }
        handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls13;
    }

    public async Task<Response<Queue?>> GetQueue(string queueName, CancellationToken cancellationToken = default)
    {
        Queue? value = null;

        var escapedQueueName = Uri.EscapeDataString(queueName);
        var response = await httpClient.GetAsync($"api/queues/{escapedVirtualHost}/{escapedQueueName}", cancellationToken)
            .ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            value = await response.Content.ReadFromJsonAsync<Queue>(cancellationToken).ConfigureAwait(false);
        }

        return new Response<Queue?>(
            response.StatusCode,
            response.ReasonPhrase ?? string.Empty,
            value);
    }

    public async Task<Response<Overview?>> GetOverview(CancellationToken cancellationToken = default)
    {
        Overview? value = null;

        var response = await httpClient.GetAsync($"api/overview", cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            value = await response.Content.ReadFromJsonAsync<Overview>(cancellationToken).ConfigureAwait(false);
        }

        return new Response<Overview?>(
            response.StatusCode,
            response.ReasonPhrase ?? string.Empty,
            value);
    }

    public async Task<Response<FeatureFlagList?>> GetFeatureFlags(CancellationToken cancellationToken = default)
    {
        FeatureFlagList? value = null;

        var response = await httpClient.GetAsync($"api/feature-flags", cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            value = await response.Content.ReadFromJsonAsync<FeatureFlagList>(cancellationToken).ConfigureAwait(false);
        }

        return new Response<FeatureFlagList?>(
            response.StatusCode,
            response.ReasonPhrase ?? string.Empty,
            value);
    }

    public async Task CreatePolicy(Policy policy, CancellationToken cancellationToken = default)
    {
        if (policy.Name == null)
        {
            throw new ArgumentNullException(nameof(policy.Name));
        }

        policy.VirtualHost = Uri.EscapeDataString(virtualHost);

        var escapedPolicyName = Uri.EscapeDataString(policy.Name);
        var response = await httpClient.PutAsJsonAsync($"api/policies/{escapedVirtualHost}/{escapedPolicyName}", policy, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }
}
