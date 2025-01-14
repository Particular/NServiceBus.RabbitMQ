#nullable enable

namespace NServiceBus.Transport.RabbitMQ.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Transport.RabbitMQ.ManagementClient;
    using NUnit.Framework;
    using NUnit.Framework.Internal;

    [TestFixture]
    class ManagementClientTests
    {
        static readonly string connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString") ?? "host=localhost";
        static readonly ConnectionConfiguration connectionConfiguration = ConnectionConfiguration.Create(connectionString);
        static readonly ConnectionFactory connectionFactory = new(typeof(ManagementClientTests).FullName, connectionConfiguration, null, false, false, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(10), []);
        ManagementClient managementClient;

        const int defaultBrokerPort = 5672;
        const int defaultBrokerTlsPort = 5671;
        const int defaultManagementPort = 15672;
        const int defaultManagementTlsPort = 15671;
        const string defaultUserName = "guest";
        const string defaultPassword = "guest";
        const string defaultVirtualHost = "/";

        [SetUp]
        public void SetUp()
        {
            var defaultManagementUrl = ManagementClient.CreateManagementConnectionString(connectionConfiguration);
            managementClient = new(defaultManagementUrl, defaultVirtualHost);
        }

        [Test]
        [TestCase("http://localhost", "guest", "guest", "http://localhost:15672")]
        [TestCase("http://localhost:15672", "guest", "guest", "http://localhost:15672")]
        [TestCase("http://copa:abc123xyz@localhost", "copa", "abc123xyz", "http://localhost:15672")]
        [TestCase("http://copa:abc123xyz@localhost", "guest", "guest", "http://localhost:15672")] // The management client will try guest:guest if the provided credentials fail first
        [TestCase("http://copa:abc123xyz@localhost:15672", "guest", "guest", "http://localhost:15672")]
        [TestCase("http://guest:guest@localhost", "guest", "guest", "http://localhost:15672")]
        [TestCase("http://guest:guest@localhost:15672", "guest", "guest", "http://localhost:15672")]
        public void ValidateManagementConnection_Should_Not_Throw_With_Default_Management_Api_Connection(
            string managementApiUrl,
            string expectedUserName,
            string expectedPassword,
            string expectedUrl)
        {
            var HttpClient = CreateFakeHttpClient(request => FakeResponses.CheckRequestMessageConnection(request, expectedUserName, expectedPassword, expectedUrl));
            managementClient = CreateManagementClient(managementApiUrl, HttpClient);

            Assert.DoesNotThrowAsync(async () => await managementClient.ValidateManagementConnection());
        }

        [Test]
        [TestCase("https://localhost", "guest", "guest", "https://localhost:15671")]
        [TestCase("https://localhost:15671", "guest", "guest", "https://localhost:15671")]
        [TestCase("https://copa:abc123xyz@localhost", "copa", "abc123xyz", "https://localhost:15671")]
        [TestCase("https://copa:abc123xyz@localhost", "guest", "guest", "https://localhost:15671")] // The management client will try guest:guest if the provided credentials fail first
        [TestCase("https://guest:guest@localhost", "guest", "guest", "https://localhost:15671")]
        [TestCase("https://guest:guest@localhost:15671", "guest", "guest", "https://localhost:15671")]
        public void ValidateManagementConnection_Should_Not_Throw_With_Default_Management_Api_Tls_Connection(
            string managementApiUrl,
            string expectedUserName,
            string expectedPassword,
            string expectedUrl)
        {
            var HttpClient = CreateFakeHttpClient(request => FakeResponses.CheckRequestMessageConnection(request, expectedUserName, expectedPassword, expectedUrl));
            var managementClient = CreateManagementClient(managementApiUrl, HttpClient);

            Assert.DoesNotThrowAsync(async () => await managementClient.ValidateManagementConnection());
        }

        [Test]
        [TestCase("http://localhost", "admin", "admin")]
        [TestCase("http://localhost:15672", "admin", "admin")]
        [TestCase("https://localhost:15671", "admin", "admin")]
        [TestCase("http://copa:abc123xyz@localhost", "admin", "admin")]
        [TestCase("http://guest:guest@localhost", "admin", "admin")]
        [TestCase("http://guest:guest@localhost:15672", "admin", "admin")]
        [TestCase("https://guest:guest@localhost:15671", "admin", "admin")]
        public void ValidateManagementConnection_Should_Throw_With_Invalid_Credentials(
            string managementApiUrl,
            string expectedUserName,
            string expectedPassword)
        {
            var httpClient = CreateFakeHttpClient(request => FakeResponses.CheckAuthentication(request, expectedUserName, expectedPassword));
            var managementClient = CreateManagementClient(managementApiUrl, httpClient);

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await managementClient.ValidateManagementConnection());
        }

        [Test]
        [TestCase("host=localhost", "guest", "guest", "http://guest:guest@localhost:15672")]
        [TestCase("host=localhost;useTls=true", "guest", "guest", "https://guest:guest@localhost:15671")]
        [TestCase("host=localhost;useTls=false", "guest", "guest", "http://guest:guest@localhost:15672")]
        [TestCase("host=localhost;port=12345;useTls=true", "guest", "guest", "https://guest:guest@localhost:15671")]
        [TestCase("host=localhost;port=12345;useTls=false", "guest", "guest", "http://guest:guest@localhost:15672")]
        [TestCase("host=localhost;username=copa;password=abc123xyz;useTls=true", "guest", "guest", "https://copa:abc123xyz@localhost:15671")]
        [TestCase("host=localhost;username=copa;password=abc123xyz;useTls=false", "guest", "guest", "http://copa:abc123xyz@localhost:15672")]
        [TestCase("host=localhost;username=copa;password=abc123xyz;useTls=true", "copa", "abc123xyz", "https://copa:abc123xyz@localhost:15671")]
        [TestCase("host=localhost;username=copa;password=abc123xyz;useTls=false", "copa", "abc123xyz", "http://copa:abc123xyz@localhost:15672")]
        [TestCase("host=localhost;username=copa;password=abc123xyz;port=12345;useTls=true", "guest", "guest", "https://copa:abc123xyz@localhost:15671")]
        [TestCase("host=localhost;username=copa;password=abc123xyz;port=12345;useTls=false", "guest", "guest", "http://copa:abc123xyz@localhost:15672")]
        [TestCase("host=localhost;username=copa;password=abc123xyz;port=12345;useTls=true", "copa", "abc123xyz", "https://copa:abc123xyz@localhost:15671")]
        [TestCase("host=localhost;username=copa;password=abc123xyz;port=12345;useTls=false", "copa", "abc123xyz", "http://copa:abc123xyz@localhost:15672")]
        public void ValidateManagementConnection_Should_Not_Throw_With_Valid_Or_Default_Broker_ConnectionConfiguration(
            string brokerConnectionString,
            string expectedUserName,
            string expectedPassword,
            string expectedUrl)
        {
            var httpClient = CreateFakeHttpClient(request => FakeResponses.CheckAuthentication(request, expectedUserName, expectedPassword));
            var connectionConfiguration = ConnectionConfiguration.Create(brokerConnectionString);
            var managementApiUrl = ManagementClient.CreateManagementConnectionString(connectionConfiguration);
            Assert.That(string.Equals(managementApiUrl, expectedUrl), Is.True);

            var managementClient = CreateManagementClient(managementApiUrl, httpClient);

            Assert.DoesNotThrowAsync(async () => await managementClient.ValidateManagementConnection());
        }

        [Test]
        [TestCase("host=localhost", "admin", "admin", "http://guest:guest@localhost:15672")]
        [TestCase("host=localhost;useTls=true", "admin", "admin", "https://guest:guest@localhost:15671")]
        [TestCase("host=localhost;useTls=false", "admin", "admin", "http://guest:guest@localhost:15672")]
        [TestCase("host=localhost;port=12345;useTls=true", "admin", "admin", "https://guest:guest@localhost:15671")]
        [TestCase("host=localhost;port=12345;useTls=false", "admin", "admin", "http://guest:guest@localhost:15672")]
        [TestCase("host=localhost;username=copa;password=abc123xyz;useTls=true", "admin", "admin", "https://copa:abc123xyz@localhost:15671")]
        [TestCase("host=localhost;username=copa;password=abc123xyz;useTls=false", "admin", "admin", "http://copa:abc123xyz@localhost:15672")]
        [TestCase("host=localhost;username=copa;password=abc123xyz;useTls=true", "admin", "admin", "https://copa:abc123xyz@localhost:15671")]
        [TestCase("host=localhost;username=copa;password=abc123xyz;useTls=false", "admin", "admin", "http://copa:abc123xyz@localhost:15672")]
        [TestCase("host=localhost;username=copa;password=abc123xyz;port=12345;useTls=true", "admin", "admin", "https://copa:abc123xyz@localhost:15671")]
        [TestCase("host=localhost;username=copa;password=abc123xyz;port=12345;useTls=false", "admin", "admin", "http://copa:abc123xyz@localhost:15672")]
        [TestCase("host=localhost;username=copa;password=abc123xyz;port=12345;useTls=true", "admin", "admin", "https://copa:abc123xyz@localhost:15671")]
        [TestCase("host=localhost;username=copa;password=abc123xyz;port=12345;useTls=false", "admin", "admin", "http://copa:abc123xyz@localhost:15672")]
        public void ValidateManagementConnection_Should_Throw_With_Invalid_Management_Credentials_From_Broker_ConnectionConfiguration(
            string brokerConnectionString,
            string expectedUserName,
            string expectedPassword,
            string expectedUrl)
        {
            var httpClient = CreateFakeHttpClient(request => FakeResponses.CheckAuthentication(request, expectedUserName, expectedPassword));
            var connectionConfiguration = ConnectionConfiguration.Create(brokerConnectionString);
            var managementApiUrl = ManagementClient.CreateManagementConnectionString(connectionConfiguration);
            Assert.That(string.Equals(managementApiUrl, expectedUrl), Is.True);

            var managementClient = CreateManagementClient(managementApiUrl, httpClient);

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await managementClient.ValidateManagementConnection());
        }

        [Test]
        public void Constructor_Should_Throw_With_Invalid_Scheme()
        {
            var managementApiUrl = "amqp:guest:guest@localhost:15672";

            var exception = Assert.Throws<NotSupportedException>(() => managementClient = new(managementApiUrl, defaultVirtualHost));
        }

        [Test]
        public async Task GetQueue_Should_Return_Queue_Information_When_Exists()
        {
            // Arrange
            var queueName = nameof(GetQueue_Should_Return_Queue_Information_When_Exists);
            await CreateQuorumQueue(queueName).ConfigureAwait(false);

            // Act
            var response = await managementClient.GetQueue(queueName);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(response.Value, Is.Not.Null);
                Assert.That(response.Value?.Name, Is.EqualTo(queueName));
            });
        }

        [Test]
        public async Task GetOverview_Should_Return_Broker_Information()
        {
            // Act
            var response = await managementClient.GetOverview();

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(response.Value, Is.Not.Null);
                Assert.That(response.Value?.ProductName, Is.EqualTo("RabbitMQ"));
                Assert.That(response.Value?.ManagementVersion.Major, Is.InRange(3, 4));
                Assert.That(response.Value?.ProductVersion.Major, Is.InRange(3, 4));
                Assert.That(response.Value?.RabbitMqVersion.Major, Is.InRange(3, 4));
            });
        }

        [Test]
        public async Task GetFeatureFlags_Should_Return_FeatureFlag_Information()
        {
            // Act
            var response = await managementClient.GetFeatureFlags();

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(response.Value, Is.Not.Null);
                Assert.That(response.Value, Is.Not.Empty);
                Assert.That(response.Value?.Contains(FeatureFlags.QuorumQueue), Is.True);
            });
        }

        [Test]
        [TestCase(-1)]
        [TestCase(200)]
        public async Task CreatePolicy_With_DeliveryLimit_Should_Be_Applied_To_Quorum_Queues(int deliveryLimit)
        {
            // Arrange
            var queueName = nameof(CreatePolicy_With_DeliveryLimit_Should_Be_Applied_To_Quorum_Queues);
            var policyName = $"{queueName} policy";
            await CreateQuorumQueue(queueName);

            // Act
            var policy = new Policy
            {
                ApplyTo = PolicyTarget.QuorumQueues,
                Definition = new PolicyDefinition
                {
                    DeliveryLimit = deliveryLimit
                },
                Name = policyName,
                Pattern = queueName,
                Priority = 100
            };
            await managementClient.CreatePolicy(policy);

            // Assert

            // It can take some time for updated policies to be applied, so we need to wait.
            // If this test is randomly failing, consider increasing the delay
            await Task.Delay(10000);
            var response = await managementClient.GetQueue(queueName);
            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(response.Value, Is.Not.Null);
                Assert.That(response.Value?.AppliedPolicyName, Is.EqualTo(policyName));
                Assert.That(response.Value?.EffectivePolicyDefinition?.DeliveryLimit, Is.EqualTo(deliveryLimit));
            });
        }

        static async Task CreateQuorumQueue(string queueName)
        {
            using var connection = await connectionFactory.CreateConnection($"{queueName} connection").ConfigureAwait(false);
            using var channel = await connection.CreateChannelAsync().ConfigureAwait(false);
            var arguments = new Dictionary<string, object?> { { "x-queue-type", "quorum" } };

            _ = await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: arguments);
        }

        static string ConvertAuthenticationToBase64String(string userName, string password)
        {
            return Convert.ToBase64String(Encoding.ASCII.GetBytes($"{userName}:{password}"));
        }

        static string DecodeAuthenticationHeader(string authenticationHeader)
        {
            byte[] data = Convert.FromBase64String(authenticationHeader);
            return Encoding.ASCII.GetString(data);
        }

        static HttpClient CreateFakeHttpClient(Func<HttpRequestMessage, HttpResponseMessage> fakeResponse) => new(new FakeHttpMessageHandler { FakeResponse = fakeResponse });

        static ManagementClient CreateManagementClient(string managementApiUrl, HttpClient httpClient) => new(defaultVirtualHost, httpClient, managementApiUrl);

        class FakeHttpMessageHandler : HttpMessageHandler
        {
            public Func<HttpRequestMessage, HttpResponseMessage>? FakeResponse { get; set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
            {
                var response = FakeResponse?.Invoke(request) ?? FakeResponses.NotFound();
                return await Task.FromResult(response);
            }
        }

        static class FakeResponses
        {
            public static HttpResponseMessage Valid() => new()
            {
                StatusCode = HttpStatusCode.OK
            };

            public static HttpResponseMessage Unauthorized() => new()
            {
                StatusCode = HttpStatusCode.Unauthorized
            };

            public static HttpResponseMessage NotFound() => new()
            {
                StatusCode = HttpStatusCode.NotFound
            };

            public static HttpResponseMessage CheckRequestMessageConnection(HttpRequestMessage request, string userName, string password, string url)
            {
                var PathAndQuery = request.RequestUri?.PathAndQuery ?? string.Empty;
                var requestUrl = request.RequestUri?.AbsoluteUri.Replace(PathAndQuery, string.Empty);
                var credentials = request.Headers.Authorization?.Parameter;
                var expectedCredentials = ConvertAuthenticationToBase64String(userName, password);
                var isValidCredential = string.Equals(expectedCredentials, credentials);
                var isValidBaseAddress = string.Equals(url, requestUrl);

                return !isValidCredential
                    ? Unauthorized()
                    : (isValidBaseAddress ? Valid() : NotFound());
            }

            public static HttpResponseMessage CheckAuthentication(HttpRequestMessage request, string userName, string password)
            {
                var credentials = request.Headers.Authorization?.Parameter;
                var expectedCredentials = ConvertAuthenticationToBase64String(userName, password);
                return string.Equals(expectedCredentials, credentials) ? Valid() : Unauthorized();
            }
        }
    }
}
