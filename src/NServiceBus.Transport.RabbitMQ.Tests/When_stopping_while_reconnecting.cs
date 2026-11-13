namespace NServiceBus.Transport.RabbitMQ.Tests
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using NUnit.Framework;

    [TestFixture]
    class When_stopping_while_reconnecting : RabbitMqContext
    {
        PausedTimeProvider timeProvider = new();

        public override TimeProvider TimeProvider => timeProvider;

        public override Task SetUp()
        {
            timeProvider = new PausedTimeProvider();
            return base.SetUp();
        }

        [Test]
        public async Task Should_stop_waiting_to_reconnect()
        {
            await CloseConnectionFromBroker($"{ReceiverQueue} MessagePump");

            // Paused timers never fire, so the pump keeps waiting to reconnect until it's stopped.
            var reconnectWait = await timeProvider.TimerCreated.WaitAsync(TimeSpan.FromSeconds(30));

            await messagePump.StopReceive();

            var consumerCount = await GetConsumerCount(ReceiverQueue);

            Assert.Multiple(() =>
            {
                Assert.That(reconnectWait.IsDisposed, Is.True, "StopReceive returned while the pump was still waiting to reconnect.");
                Assert.That(consumerCount, Is.Zero);
            });
        }

        async Task CloseConnectionFromBroker(string connectionName)
        {
            using var managementClient = CreateManagementHttpClient();

            // The management API lists a new connection with a delay.
            var timeout = DateTime.UtcNow + TimeSpan.FromSeconds(30);
            JsonElement? connection = null;

            while (connection is null && DateTime.UtcNow < timeout)
            {
                var connections = await managementClient.GetFromJsonAsync<JsonElement[]>("api/connections");
                connection = connections!
                    .Where(c =>
                        c.TryGetProperty("client_properties", out var clientProperties) &&
                        clientProperties.TryGetProperty("connection_name", out var name) &&
                        name.GetString() == connectionName)
                    .Select(c => (JsonElement?)c)
                    .SingleOrDefault();

                if (connection is null)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500));
                }
            }

            Assert.That(connection, Is.Not.Null, $"The broker did not list the connection '{connectionName}'.");

            using var response = await managementClient.DeleteAsync($"api/connections/{Uri.EscapeDataString(connection!.Value.GetProperty("name").GetString()!)}");
            response.EnsureSuccessStatusCode();
        }

        async Task<uint> GetConsumerCount(string queueName)
        {
            await using var connection = await connectionFactory.CreateAdministrationConnection();
            await using var channel = await connection.CreateChannelAsync();

            var queue = await channel.QueueDeclarePassiveAsync(queueName);
            return queue.ConsumerCount;
        }

        static HttpClient CreateManagementHttpClient()
        {
            var connectionConfiguration = ConnectionConfiguration.Create(Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString") ?? "host=localhost");

            var baseAddress = new UriBuilder
            {
                Scheme = connectionConfiguration.UseTls ? "https" : "http",
                Host = connectionConfiguration.Host,
                Port = connectionConfiguration.UseTls ? 15671 : 15672
            }.Uri;

            var handler = new HttpClientHandler
            {
                Credentials = new NetworkCredential(connectionConfiguration.UserName, connectionConfiguration.Password),
                PreAuthenticate = true
            };

            return new HttpClient(handler) { BaseAddress = baseAddress };
        }

        sealed class PausedTimeProvider : TimeProvider
        {
            readonly TaskCompletionSource<PausedTimer> timerCreated = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task<PausedTimer> TimerCreated => timerCreated.Task;

            public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
            {
                var timer = new PausedTimer();
                timerCreated.TrySetResult(timer);
                return timer;
            }
        }

        sealed class PausedTimer : ITimer
        {
            public bool IsDisposed { get; private set; }

            public bool Change(TimeSpan dueTime, TimeSpan period) => !IsDisposed;

            public void Dispose() => IsDisposed = true;

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
