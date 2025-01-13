#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementClient;

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
    HttpClient httpClient;
    readonly string virtualHost;
    readonly string escapedVirtualHost;

    const int defaultManagementPort = 15672;
    const int defaultManagementTlsPort = 15671;
    const string defaultVirtualHost = "/";
    const string defaultUserName = "guest";
    const string defaultPassword = "guest";

    public ManagementClient(string managementApiUrl, string virtualHost)
    {
        ArgumentNullException.ThrowIfNull(managementApiUrl, nameof(managementApiUrl));

        var managementUri = GenerateUri(managementApiUrl);

        var scheme = managementUri.Scheme;
        var host = managementUri.Host;
        var port = managementUri.Port;
        var userName = managementUri.UserName;
        var password = managementUri.Password;

        this.virtualHost = virtualHost;
        escapedVirtualHost = Uri.EscapeDataString(virtualHost);

        httpClient = CreateHttpClient(scheme, host, port, userName, password);
    }

    public ManagementClient(ConnectionConfiguration connectionConfiguration)
    {
        ArgumentNullException.ThrowIfNull(connectionConfiguration, nameof(connectionConfiguration));

        var scheme = connectionConfiguration.UseTls ? "https" : "http";
        var host = connectionConfiguration.Host ?? "localhost";
        var port = connectionConfiguration.UseTls ? defaultManagementTlsPort : defaultManagementPort;
        var userName = connectionConfiguration.UserName ?? defaultUserName;
        var password = connectionConfiguration.Password ?? defaultPassword;

        virtualHost = connectionConfiguration.VirtualHost ?? defaultVirtualHost;
        escapedVirtualHost = Uri.EscapeDataString(virtualHost);

        httpClient = CreateHttpClient(scheme, host, port, userName, password);
    }

    // This is used for testing
    public ManagementClient(string virtualHost, HttpClient httpClient, string managementApiUrl)
    {
        var managementUri = GenerateUri(managementApiUrl);

        var userName = managementUri.UserName;
        var password = managementUri.Password;

        // Clear these values for setting the baseAddress of the httpClient
        managementUri.UserName = string.Empty;
        managementUri.Password = string.Empty;

        this.httpClient = httpClient;
        httpClient.BaseAddress = managementUri.Uri;
        SetManagementClientAuthorization(httpClient, userName, password);

        this.virtualHost = virtualHost;
        escapedVirtualHost = Uri.EscapeDataString(virtualHost);
    }

    public async Task ValidateManagementConnection(CancellationToken cancellationToken = default)
    {
        // Check broker provided authentication
        var (isValid, _) = await IsConnectionValid(cancellationToken).ConfigureAwait(false);
        if (isValid)
        {
            return;
        }

        // Check default management authentication
        SetManagementClientAuthorization(httpClient, defaultUserName, defaultPassword);

        var (isDefaultValid, exception) = await IsConnectionValid(cancellationToken).ConfigureAwait(false);
        if (exception != null || !isDefaultValid)
        {
            throw exception ?? new InvalidOperationException($"Connection to the management API could not be established with the default or provided URL. Update the RabbitMQTransport.ManagementApiUrl with the correct HTTP connection string.");
        }
    }

    public static string CreateManagementConnectionString(ConnectionConfiguration connectionConfiguration)
    {
        var scheme = connectionConfiguration.UseTls ? "https" : "http";
        var userName = connectionConfiguration.UserName ?? defaultUserName;
        var password = connectionConfiguration.Password ?? defaultPassword;
        var host = connectionConfiguration.Host ?? "localhost";
        var port = connectionConfiguration.UseTls ? defaultManagementTlsPort : defaultManagementPort;
        return $"{scheme}://{userName}:{password}@{host}:{port}";
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

        policy.VirtualHost = virtualHost;

        var escapedPolicyName = Uri.EscapeDataString(policy.Name);
        var response = await httpClient.PutAsJsonAsync($"api/policies/{escapedVirtualHost}/{escapedPolicyName}", policy, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }

    UriBuilder GenerateUri(string managementApiUrl)
    {
        var dictionary = ParseManagementApiUrl(managementApiUrl);

        var invalidOptionsMessage = new StringBuilder();

        var scheme = GetValue(dictionary, "scheme", "http");
        ValidateScheme(scheme, invalidOptionsMessage);

        var useTls = scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
        var host = GetValue(dictionary, "host", "localhost");
        var port = GetValue(dictionary, "port", int.TryParse, useTls ? defaultManagementTlsPort : defaultManagementPort, invalidOptionsMessage);
        var userName = GetValue(dictionary, "userName", defaultUserName);
        var password = GetValue(dictionary, "password", defaultPassword);

        if (invalidOptionsMessage.Length > 0)
        {
            throw new NotSupportedException(invalidOptionsMessage.ToString().TrimEnd('\r', '\n'));
        }

        return new UriBuilder
        {
            Scheme = scheme,
            Host = host,
            Port = port,
            UserName = userName,
            Password = password,
        };
    }

    static void ValidateScheme(string scheme, StringBuilder invalidOptionsMessage)
    {
        if (!scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
            !scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            _ = invalidOptionsMessage.AppendLine("Invalid scheme for RabbitMQ management API, use either 'http' or 'https' in the ManagementApiUrl");
        }
    }

    static string GetValue(Dictionary<string, string> dictionary, string key, string defaultValue)
    {
        return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
    }

    delegate bool Convert<T>(string input, out T output);

    static T GetValue<T>(Dictionary<string, string> dictionary, string key, Convert<T> convert, T defaultValue, StringBuilder invalidOptionsMessage)
    {
        if (dictionary.TryGetValue(key, out var value))
        {
            if (!convert(value, out defaultValue))
            {
                invalidOptionsMessage.AppendLine($"'{value}' is not a valid {typeof(T).Name} value for the '{key}' connection string option.");
            }
        }

        return defaultValue;
    }

    HttpClient CreateHttpClient(string scheme, string host, int port, string userName, string password)
    {
        var uriBuilder = new UriBuilder
        {
            Scheme = scheme,
            Host = host,
            Port = port,
        };

        var client = new HttpClient { BaseAddress = uriBuilder.Uri };
        SetManagementClientAuthorization(client, userName, password);
        return client;
    }

    static Dictionary<string, string> ParseManagementApiUrl(string managementApiUrl)
    {
        var dictionary = new Dictionary<string, string>();
        if (!Uri.TryCreate(managementApiUrl, UriKind.Absolute, out var uri))
        {
            throw new UriFormatException($"The RabbitMQTransport.ManagementApiUrl is not a valid URI format.");
        }
        var useTls = uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);

        var isPortSpecified = managementApiUrl.Contains($":{uri.Port}");
        var port = !isPortSpecified && uri.IsDefaultPort
            ? (useTls ? defaultManagementTlsPort : defaultManagementPort)
            : uri.Port;

        var userInfo = uri.UserInfo.Split(':') ?? [];

        dictionary.Add("scheme", uri.Scheme);
        dictionary.Add("host", uri.Host);
        dictionary.Add("port", port.ToString());
        dictionary.Add("userName", userInfo.Length > 0 && !string.IsNullOrEmpty(userInfo[0]) ? userInfo[0] : defaultUserName);
        dictionary.Add("password", userInfo.Length > 1 && !string.IsNullOrEmpty(userInfo[1]) ? userInfo[1] : defaultPassword);

        return dictionary;
    }

    void SetManagementClientAuthorization(HttpClient client, string userName, string password) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{userName}:{password}")));

    async Task<(bool IsValid, Exception? ex)> IsConnectionValid(CancellationToken cancellationToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Head, $"{httpClient.BaseAddress}api/overview");
            var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return (response.IsSuccessStatusCode, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return (false, ex);
        }
    }
}
