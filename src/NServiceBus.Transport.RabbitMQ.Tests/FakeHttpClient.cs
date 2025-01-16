#nullable enable

namespace NServiceBus.Transport.RabbitMQ.Tests
{
    using System;
    using System.Net.Http;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using ManagementClient;
    using Queue = ManagementClient.Queue;
    using System.Text.Json;

    class FakeHttpClient
    {
        public bool Testing { get; set; }

        public static string ConvertAuthenticationToBase64String(string userName, string password) =>
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{userName}:{password}"));

        public static string DecodeAuthenticationHeader(string authenticationHeader)
        {
            var data = Convert.FromBase64String(authenticationHeader);
            return Encoding.ASCII.GetString(data);
        }

        public static HttpClient CreateFakeHttpClient(Func<HttpRequestMessage, HttpResponseMessage> fakeResponse) => new(new FakeHttpMessageHandler { FakeResponse = fakeResponse }, true);

        public class FakeHttpMessageHandler : HttpMessageHandler
        {
            public Func<HttpRequestMessage, HttpResponseMessage>? FakeResponse { get; set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
            {
                //request?.RequestUri?.PathAndQuery.Contains("api/policies/");
                var response = FakeResponse?.Invoke(request) ?? FakeResponses.NotFound();
                return await Task.FromResult(response);
            }
        }

        public static class FakeResponses
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

            public static HttpResponseMessage GetQueue(QueueType queueType = QueueType.Quorum, int deliveryLimit = 20)
            {
                var queue = new Queue
                {
                    Name = "test",
                    Arguments = new QueueArguments()
                    {
                        QueueType = queueType,
                    },
                    DeliveryLimit = deliveryLimit
                };

                var json = JsonSerializer.Serialize(queue);
                var httpContent = new StringContent(json);
                var response = new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = httpContent
                };
                return response;
            }

            public static HttpResponseMessage GetOverview(
                HttpRequestMessage request,
                string? expectedUserName = null,
                string? expectedPassword = null,
                string? expectedUrl = null)
            {

                var response = CheckRequestMessageConnection(request, expectedUserName, expectedPassword, expectedUrl);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    return response;
                }
                var overview = new Overview
                {
                    ClusterName = "rabbit@my - rabbit",
                    ProductName = "RabbitMQ",
                    ProductVersion = new Version(4, 0, 3),
                    ManagementVersion = new Version(4, 0, 3),
                    RabbitMqVersion = new Version(4, 0, 3),
                    Node = "rabbit@my-rabbit"
                };

                var json = JsonSerializer.Serialize(overview);
                var httpContent = new StringContent(json);
                response.Content = httpContent;
                return response;
            }

            public static HttpResponseMessage CheckRequestMessageConnection(
                HttpRequestMessage request,
                string? expectedUserName = null,
                string? expectedPassword = null,
                string? expectedUrl = null)
            {
                var PathAndQuery = request.RequestUri?.PathAndQuery ?? string.Empty;
                var requestUrl = request.RequestUri?.AbsoluteUri.Replace(PathAndQuery, string.Empty);
                var credentials = request.Headers.Authorization?.Parameter;
                var expectedCredentials = ConvertAuthenticationToBase64String(expectedUserName ?? "guest", expectedPassword ?? "guest");
                var isValidCredential = string.Equals(expectedCredentials, credentials);
                var isValidBaseAddress = string.Equals(expectedUrl ?? "http://localhost:15672", requestUrl);

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
