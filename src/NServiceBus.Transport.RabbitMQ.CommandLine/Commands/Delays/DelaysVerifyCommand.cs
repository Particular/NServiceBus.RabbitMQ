namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System;
    using System.CommandLine;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    class DelaysVerifyCommand
    {
        public static Command CreateCommand()
        {
            var command = new Command("verify", "Verify broker requirements for using the v2 delay infrastructure");

            var urlOption = new Option<string>("--url", "The URL of the RabbitMQ management API")
            {
                IsRequired = true
            };

            var usernameOption = new Option<string>("--username", "The username for accessing the RabbitMQ management API")
            {
                IsRequired = true
            };

            var passwordOption = new Option<string>("--password", "The password for accessing the RabbitMQ management API")
            {
                IsRequired = true
            };

            command.AddOption(urlOption);
            command.AddOption(usernameOption);
            command.AddOption(passwordOption);

            command.SetHandler(async (url, username, password, console, cancellationToken) =>
            {
                var delaysVerify = new DelaysVerifyCommand(url, username, password, console);
                await delaysVerify.Run(cancellationToken).ConfigureAwait(false);
            },
            urlOption, usernameOption, passwordOption, Bind.FromServiceProvider<IConsole>(), Bind.FromServiceProvider<CancellationToken>());

            return command;
        }

        public DelaysVerifyCommand(string baseUrl, string username, string password, IConsole console)
        {
            this.baseUrl = baseUrl;
            this.username = username;
            this.password = password;
            this.console = console;
        }

        public async Task Run(CancellationToken cancellationToken = default)
        {
            using var httpClient = new HttpClient();

            var authString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authString);

            var serverDetails = await GetServerDetails(httpClient, cancellationToken).ConfigureAwait(false);

            if (Version.TryParse(serverDetails.Overview?.ProductVersion, out var version) && version < Version.Parse("3.10.0"))
            {
                throw new Exception($"Fail: Detected broker version is {serverDetails.Overview.ProductVersion}, at least 3.10.0 is required");
            }

            var streamQueueState = serverDetails.FeatureFlags?.SingleOrDefault(fs => fs.Name == "stream_queue");

            if (streamQueueState == null || !streamQueueState.IsEnabled())
            {
                throw new Exception($"Fail: stream_queue feature flag is not enabled");
            }

            var quorumQueueState = serverDetails.FeatureFlags?.SingleOrDefault(fs => fs.Name == "quorum_queue");

            if (quorumQueueState == null || !quorumQueueState.IsEnabled())
            {
                throw new Exception($"Fail: quorum_queue feature flag is not enabled");
            }

            console.WriteLine("All checks OK");
        }

        async Task<ServerDetails> GetServerDetails(HttpClient httpClient, CancellationToken cancellationToken)
        {
            return new ServerDetails
            {
                Overview = await MakeHttpRequest<Overview>(httpClient, "overview", cancellationToken).ConfigureAwait(false),
                FeatureFlags = await MakeHttpRequest<FeatureFlag[]>(httpClient, "feature-flags", cancellationToken).ConfigureAwait(false)
            };
        }

        async Task<T?> MakeHttpRequest<T>(HttpClient httpClient, string urlPart, CancellationToken cancellationToken)
        {
            var url = $"{baseUrl}/api/{urlPart}";
            using var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrEmpty(content))
            {
                throw new Exception("Empty response returned for " + url);
            }

            return JsonSerializer.Deserialize<T>(content);
        }

        readonly string baseUrl;
        readonly string username;
        readonly string password;
        readonly IConsole console;

        class ServerDetails
        {
            public Overview? Overview { get; set; }

            public FeatureFlag[]? FeatureFlags { get; set; }
        }

        class Overview
        {
            [JsonPropertyName("product_version")]
            public string ProductVersion { get; set; } = string.Empty;
        }

        class FeatureFlag
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("state")]
            public string State { get; set; } = string.Empty;

            public bool IsEnabled()
            {
                return State?.ToLower() == "enabled";
            }
        }
    }
}