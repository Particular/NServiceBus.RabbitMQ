#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementClient;

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

class ManagementClient
{
    HttpClient httpClient;

    public int Port { get; private set; }
    public string UserName { get; private set; }
    public string Password { get; private set; }

    public readonly string Host;
    public readonly bool UseTls;
    public readonly string VirtualHost;

    readonly ConnectionConfiguration ConnectionConfiguration;
    readonly string escapedVirtualHost;

    const int defaultManagementPort = 15672;
    const int defaultManagementTlsPort = 15671;
    const string defaultUserName = "guest";
    const string defaultPassword = "guest";

    public ManagementClient(ConnectionConfiguration connectionConfiguration)
    {
        ArgumentNullException.ThrowIfNull(connectionConfiguration, nameof(connectionConfiguration));
        ConnectionConfiguration = connectionConfiguration;

        Host = ConnectionConfiguration.Host;
        VirtualHost = ConnectionConfiguration.VirtualHost;
        escapedVirtualHost = Uri.EscapeDataString(VirtualHost);
        Port = ConnectionConfiguration.Port;
        UserName = ConnectionConfiguration.UserName;
        Password = ConnectionConfiguration.Password;
        UseTls = ConnectionConfiguration.UseTls;

        httpClient = CreateHttpClient(Port, UserName, Password);
    }

    public async Task ValidateConnectionConfiguration(CancellationToken cancellationToken = default)
    {
        if (await IsConnectionValid(cancellationToken).ConfigureAwait(false)
            || await IsConnectionValidWithDefaultPort(cancellationToken).ConfigureAwait(false)
            || await IsConnectionValidWithDefaultAuthorization(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        SetDefaultManagementPort();
        SetManagementAuthorization(httpClient, defaultUserName, defaultPassword);

        if (await IsConnectionValid(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        throw new HttpRequestException("The management connection configuration could not be validated with the supplied connection string or the default values.");
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
        ArgumentNullException.ThrowIfNull(policy, nameof(policy));

        policy.VirtualHost = VirtualHost;

        var escapedPolicyName = Uri.EscapeDataString(policy.Name);
        var response = await httpClient.PutAsJsonAsync($"api/policies/{escapedVirtualHost}/{escapedPolicyName}", policy, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }

    HttpClient CreateHttpClient(int port, string userName, string password)
    {
        var uriBuilder = new UriBuilder
        {
            Scheme = UseTls ? "https" : "http",
            Host = Host,
            Port = port,
        };

        Port = port;
        UserName = userName ?? defaultUserName;
        Password = password ?? defaultPassword;
        var client = new HttpClient { BaseAddress = uriBuilder.Uri };
        SetManagementAuthorization(client, UserName, Password);
        return client;
    }

    void SetManagementAuthorization(HttpClient client, string userName, string password) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{userName}:{password}")));

    void UpdateHttpClientPort(int port) => httpClient = CreateHttpClient(port, UserName, Password);

    void SetDefaultManagementPort()
    {
        if (ConnectionConfiguration.UseTls)
        {
            UpdateHttpClientPort(defaultManagementTlsPort);
            return;
        }
        UpdateHttpClientPort(defaultManagementPort);
    }

    async Task<bool> IsConnectionValid(CancellationToken cancellationToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Head, httpClient.BaseAddress);
            var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    async Task<bool> IsConnectionValidWithDefaultPort(CancellationToken cancellationToken)
    {
        SetDefaultManagementPort();

        if (await IsConnectionValid(cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        UpdateHttpClientPort(ConnectionConfiguration.Port);

        return false;
    }

    async Task<bool> IsConnectionValidWithDefaultAuthorization(CancellationToken cancellationToken)
    {
        SetManagementAuthorization(httpClient, defaultUserName, defaultPassword);
        if (await IsConnectionValid(cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        SetManagementAuthorization(httpClient, ConnectionConfiguration.UserName, ConnectionConfiguration.Password);
        return false;
    }
}
