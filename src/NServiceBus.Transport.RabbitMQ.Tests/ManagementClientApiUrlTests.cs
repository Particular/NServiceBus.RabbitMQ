using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Transport.RabbitMQ;
using NServiceBus.Transport.RabbitMQ.ManagementApi;
using NUnit.Framework;

[TestFixture]
public class ManagementClientApiUrlTests
{
    TestableManagementClient client = null!;

    [SetUp]
    public void Setup() => client = new TestableManagementClient();

    [TearDown]
    public void Teardown() => client.Dispose();

    [Test]
    public async Task CreatePolicy_PutsToCorrectUrl()
    {
        await client.CreatePolicy("my-policy", new Policy
        {
            Pattern = "my-pattern",
            ApplyTo = PolicyTarget.Queues,
            Definition = new PolicyDefinition()

        });

        client.VerifyPut("api/policies/%2Fvhosttest/my-policy");
    }

    [Test]
    public async Task GetBindingsForExchange_GetsFromCorrectUrl()
    {
        await client.GetBindingsForExchange("my-exchange");

        client.VerifyGet("api/exchanges/%2Fvhosttest/my-exchange/bindings/destination");
    }

    [Test]
    public async Task GetBindingsForQueue_GetsFromCorrectUrl()
    {
        await client.GetBindingsForQueue("my-queue");

        client.VerifyGet("api/queues/%2Fvhosttest/my-queue/bindings");
    }

    [Test]
    public async Task GetFeatureFlags_GetsFromCorrectUrl()
    {
        await client.GetFeatureFlags();

        client.VerifyGet("api/feature-flags");
    }

    [Test]
    public async Task GetOverview_GetsFromCorrectUrl()
    {
        await client.GetOverview();

        client.VerifyGet("api/overview");
    }

    [Test]
    public async Task GetQueue_GetsFromCorrectUrl()
    {
        await client.GetQueue("my-queue");

        client.VerifyGet("api/queues/%2Fvhosttest/my-queue");
    }

    [Test]
    public async Task GetQueues_GetsFromCorrectUrlWithPagination()
    {
        await client.GetQueues(page: 2, pageSize: 100);

        client.VerifyGet("api/queues/%2Fvhosttest/?page=2&page_size=100");
    }

    [Test]
    public async Task GetQueues_LastPage_ReturnsMorePagesFalse()
    {
        client.SetupGetResult(new GetQueuesResult
        {
            Items = [],
            Page = 5,
            PageCount = 5
        });

        await client.GetQueues(page: 5, pageSize: 100);

        client.VerifyGet("api/queues/%2Fvhosttest/?page=5&page_size=100");
    }

    [Test]
    public async Task GetQueue_WithSpecialCharacters_EscapesCorrectly()
    {
        await client.GetQueue("queue/with/slashes");

        client.VerifyGet("api/queues/%2Fvhosttest/queue%2Fwith%2Fslashes");
    }

    class TestableManagementClient()
        : ManagementClient(
            ConnectionConfiguration.Create("host=.;virtualHost=/vhosttest;username=u;password=p;port=12345"),
            new ManagementApiConfiguration("http://localhost:12345/xxx", "u", "p")
        )
    {
        string capturedGetUrl;
        string capturedPutUrl;
        object getResult;

        public void SetupGetResult<T>(T result) => getResult = result;

        protected override Task<T> Get<T>(string url, CancellationToken cancellationToken = default)
        {
            capturedGetUrl = url;

            if (getResult is T typedResult)
            {
                return Task.FromResult(typedResult);
            }

            return Task.FromResult(CreateDefaultResult<T>());
        }

        protected override Task Put<T>(string url, T value, CancellationToken cancellationToken = default)
        {
            capturedPutUrl = url;
            return Task.CompletedTask;
        }

        public void VerifyGet(string expectedUrl) => Assert.That(capturedGetUrl, Is.EqualTo(expectedUrl), $"Expected GET to {expectedUrl}, but got {capturedGetUrl}");

        public void VerifyPut(string expectedUrl) => Assert.That(capturedPutUrl, Is.EqualTo(expectedUrl), $"Expected PUT to {expectedUrl}, but got {capturedPutUrl}");

        static T CreateDefaultResult<T>()
        {
            if (typeof(T) == typeof(GetQueuesResult))
            {
                return (T)(object)new GetQueuesResult
                {
                    Items = [],
                    Page = 1,
                    PageCount = 1
                };
            }

            return default;
        }
    }
}