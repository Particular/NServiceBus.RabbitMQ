namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System;
    using System.CommandLine;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    class VerifySafeDelaysCommand
    {
        public static Command CreateCommand()
        {
            var verifyCommand = new Command("verify-safe-delays", "Verifies that the broker configuration allows for safe message delays.");

            var urlOption = new Option<string>("--url", "The url for the management UI of the RabbitMQ broker")
            {
                IsRequired = true
            };

            var usernameOption = new Option<string>("--username", "The username")
            {
                IsRequired = true
            };

            var passwordOption = new Option<string>("--password", "The password")
            {
                IsRequired = true
            };

            verifyCommand.AddOption(urlOption);
            verifyCommand.AddOption(usernameOption);
            verifyCommand.AddOption(passwordOption);

            verifyCommand.SetHandler(async (string url, string username, string password, CancellationToken cancellationToken) =>
            {
                var verifyProcess = new VerifySafeDelaysCommand(url, username, password);
                await verifyProcess.Execute(cancellationToken).ConfigureAwait(false);
            }, urlOption, usernameOption, passwordOption);

            return verifyCommand;
        }


        public VerifySafeDelaysCommand(string baseUrl, string username, string password)
        {
            this.baseUrl = baseUrl;
            this.username = username;
            this.password = password;
        }

        public async Task Execute(CancellationToken cancellationToken = default)
        {
            using var httpClient = new HttpClient();
            var authString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));

            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authString);

            var serverDetails = await GetServerDetails(httpClient, cancellationToken).ConfigureAwait(false);

            if (Version.Parse(serverDetails.Overview.ProductVersion) < Version.Parse("3.10.0"))
            {
                Console.WriteLine($"Fail: Detected broker version is {serverDetails.Overview.ProductVersion}, at least 3.10.0 is required");
                return;
            }

            var streamQueueState = serverDetails.FeatureFlags.SingleOrDefault(fs => fs.Name == "stream_queue");

            if (streamQueueState == null || !streamQueueState.IsEnabled())
            {
                Console.WriteLine($"Fail: stream_queue feature flag is not enabled");
                return;
            }

            var quorumQueueState = serverDetails.FeatureFlags.SingleOrDefault(fs => fs.Name == "quorum_queue");

            if (quorumQueueState == null || !quorumQueueState.IsEnabled())
            {
                Console.WriteLine($"Fail: quorum_queue feature flag is not enabled");
                return;
            }

            Console.WriteLine("All checks OK");
        }

        async Task<ServerDetails> GetServerDetails(HttpClient httpClient, CancellationToken cancellationToken)
        {
            return new ServerDetails
            {
                Overview = await MakeHttpRequest<Overview>(httpClient, "overview", cancellationToken).ConfigureAwait(false),
                FeatureFlags = await MakeHttpRequest<FeatureFlag[]>(httpClient, "feature-flags", cancellationToken).ConfigureAwait(false)
            };
        }

        async Task<T> MakeHttpRequest<T>(HttpClient httpClient, string urlPart, CancellationToken cancellationToken)
        {
            var url = $"{baseUrl}/api/{urlPart}";
            using var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrEmpty(content))
            {
                throw new Exception("Empty response returned for " + url);
            }

#pragma warning disable CS8603 // Possible null reference return.
            return JsonSerializer.Deserialize<T>(content);
#pragma warning restore CS8603 // Possible null reference return.
        }

        readonly string baseUrl;
        readonly string username;
        readonly string password;

#pragma warning disable 0649
#pragma warning disable 8618

        class ServerDetails
        {
            public Overview Overview { get; set; }
            public FeatureFlag[] FeatureFlags { get; set; }
        }

        class Overview
        {
            [JsonPropertyName("product_version")]
            public string ProductVersion { get; set; }
        }

        class FeatureFlag
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("state")]
            public string State { get; set; }

            public bool IsEnabled()
            {
                return State?.ToLower() == "enabled";
            }
        }
    }
#pragma warning restore 8618
#pragma warning restore 0649
}