using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Transport.RabbitMQ;
using NServiceBus.Transport.RabbitMQ.ManagementApi;
using NUnit.Framework;

[TestFixture]
public class ManagementClientApiUrlTests
{
    TestHttpHandler handler = null!;
    ManagementClient client = null!;

    [SetUp]
    public void Setup()
    {
        handler = new TestHttpHandler();
        var httpClient = new HttpClient(handler);
        client = new ManagementClient(
            httpClient,
            ConnectionConfiguration.Create("host=localhost;virtualHost=/vhosttest;username=guest;password=guest"),
            new ManagementApiConfiguration("http://localhost:15672/xxx")
        );
    }

    [TearDown]
    public void Teardown()
    {
        client.Dispose();
        handler.Dispose();
    }

    [Test]
    public async Task CreatePolicy_PutsToCorrectUrl()
    {
        handler.Expect(HttpMethod.Put, "/xxx/api/policies/%2Fvhosttest/my-policy");

        await client.CreatePolicy("my-policy", new Policy
        {
            Pattern = "my-pattern",
            ApplyTo = PolicyTarget.Queues,
            Definition = new PolicyDefinition()

        });

        handler.Verify();
    }

    [Test]
    public async Task GetBindingsForExchange_GetsFromCorrectUrl()
    {
        handler.Expect(HttpMethod.Get, "/xxx/api/exchanges/%2Fvhosttest/my-exchange/bindings/destination")
            .Returns(new List<Binding>());

        await client.GetBindingsForExchange("my-exchange");

        handler.Verify();
    }

    [Test]
    public async Task GetBindingsForQueue_GetsFromCorrectUrl()
    {
        handler.Expect(HttpMethod.Get, "/xxx/api/queues/%2Fvhosttest/my-queue/bindings")
            .Returns(new List<Binding>());

        await client.GetBindingsForQueue("my-queue");

        handler.Verify();
    }

    [Test]
    public async Task GetFeatureFlags_GetsFromCorrectUrl()
    {
        handler.Expect(HttpMethod.Get, "/xxx/api/feature-flags")
            .Returns(new List<FeatureFlag>());

        await client.GetFeatureFlags();

        handler.Verify();
    }

    [Test]
    public async Task GetOverview_GetsFromCorrectUrl()
    {
        handler.Expect(HttpMethod.Get, "/xxx/api/overview")
            .Returns(new Overview
            {
                BrokerVersion = "1.2.3",
                DisableStats = false,
            });

        await client.GetOverview();

        handler.Verify();
    }

    [Test]
    public async Task GetQueue_GetsFromCorrectUrl()
    {
        handler.Expect(HttpMethod.Get, "/xxx/api/queues/%2Fvhosttest/my-queue")
            .Returns(new Queue
            {
                Name = "my-queue",
                Arguments = new QueueArguments()
            });

        var result = await client.GetQueue("my-queue");

        Assert.That(result.Name, Is.EqualTo("my-queue"));
        handler.Verify();
    }

    [Test]
    public async Task GetQueues_GetsFromCorrectUrlWithPagination()
    {
        handler.Expect(HttpMethod.Get, "/xxx/api/queues/%2Fvhosttest/?page=2&page_size=100")
            .Returns(new GetQueuesResult
            {
                Items = [],
                Page = 2,
                PageCount = 5
            });

        var (_, morePages) = await client.GetQueues(page: 2, pageSize: 100);

        Assert.That(morePages, Is.True);
        handler.Verify();
    }

    [Test]
    public async Task GetQueues_LastPage_ReturnsMorePagesFalse()
    {
        handler.Expect(HttpMethod.Get, "/xxx/api/queues/%2Fvhosttest/?page=5&page_size=100")
            .Returns(new GetQueuesResult
            {
                Items = [],
                Page = 5,
                PageCount = 5
            });

        var (_, morePages) = await client.GetQueues(page: 5, pageSize: 100);

        Assert.That(morePages, Is.False);
        handler.Verify();
    }

    [Test]
    public async Task GetQueue_WithSpecialCharacters_EscapesCorrectly()
    {
        handler.Expect(HttpMethod.Get, "/xxx/api/queues/%2Fvhosttest/queue%2Fwith%2Fslashes")
            .Returns(new Queue
            {
                Name = "queue/with/slashes",
                Arguments = new QueueArguments()
            });

        await client.GetQueue("queue/with/slashes");

        handler.Verify();
    }

    class TestHttpHandler : HttpMessageHandler
    {
        HttpMethod expectedMethod;
        string expectedPath;
        object responseObject;
        bool requestMade;

        public TestHttpHandler Expect(HttpMethod method, string path)
        {
            expectedMethod = method;
            expectedPath = path;
            return this;
        }

        public TestHttpHandler Returns(object obj)
        {
            responseObject = obj;
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
#pragma warning disable PS0003
            CancellationToken cancellationToken
#pragma warning restore PS0003
        )
        {
            requestMade = true;

            Assert.That(request.Method, Is.EqualTo(expectedMethod),
                $"Expected {expectedMethod}, got {request.Method}");
            Assert.That(request.RequestUri?.PathAndQuery, Is.EqualTo(expectedPath),
                $"Expected {expectedPath}, got {request.RequestUri?.PathAndQuery}");

            var response = new HttpResponseMessage(HttpStatusCode.OK);
            if (responseObject is not null)
            {
                response.Content = JsonContent.Create(responseObject);
            }

            return Task.FromResult(response);
        }

        public void Verify() => Assert.That(requestMade, Is.True, "Expected HTTP request was not made");
    }
}