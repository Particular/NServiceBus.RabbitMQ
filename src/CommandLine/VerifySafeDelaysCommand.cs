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

            verifyCommand.SetHandler(async (CancellationToken cancellationToken) =>
            {
                var verifyProcess = new VerifySafeDelaysCommand();
                await verifyProcess.Execute(cancellationToken).ConfigureAwait(false);

            });

            return verifyCommand;
        }

        public async Task Execute(CancellationToken cancellationToken = default)
        {
            using var httpClient = new HttpClient();
            var authString = Convert.ToBase64String(Encoding.UTF8.GetBytes("guest:guest"));

            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authString);
            var failures = new List<string>();

            await CheckServerVersion(httpClient, failures, cancellationToken).ConfigureAwait(false);

            if (failures.Any())
            {
                Console.WriteLine("The following issues where detected:");

                foreach (var failure in failures)
                {
                    Console.WriteLine($"  - {failure}");
                }
            }
            else
            {
                Console.WriteLine("All checks OK");
            }
        }

        async Task CheckServerVersion(HttpClient httpClient, ICollection<string> failures, CancellationToken cancellationToken)
        {
            using var response = await httpClient.GetAsync("http://localhost:15672/api/overview", cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();


            var overviewContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            var overviewResponse = JsonSerializer.Deserialize<OverviewResponse>(overviewContent);

            if (overviewResponse == null)
            {
                failures.Add("No server version could be detected");
            }
            else
            {
                if (Version.Parse(overviewResponse.ProductVersion) < Version.Parse("3.10.0"))
                {
                    failures.Add($"Detected broker version is {overviewResponse.ProductVersion}, at least 3.10.0 is required");
                }
            }
        }

        class OverviewResponse
        {
#pragma warning disable 0649
#pragma warning disable 8618
            [JsonPropertyName("product_version")]
            public string ProductVersion { get; set; }
#pragma warning restore 8618
#pragma warning restore 0649
        }
    }
}