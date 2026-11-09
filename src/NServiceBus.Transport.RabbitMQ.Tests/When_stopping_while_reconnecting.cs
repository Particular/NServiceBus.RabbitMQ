namespace NServiceBus.Transport.RabbitMQ.Tests
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using NUnit.Framework;

    [TestFixture]
    class When_stopping_while_reconnecting : RabbitMqContext
    {
        public override TimeSpan? NetworkRecoveryInterval => TimeSpan.FromSeconds(3);

        [Test]
        public async Task Should_not_reconnect_after_the_pump_stops()
        {
            await CloseConnectionFromBroker($"{ReceiverQueue} MessagePump");

            // Gives the pump time to observe the lost connection and start waiting to reconnect.
            await Task.Delay(TimeSpan.FromSeconds(1));

            await messagePump.StopReceive();

            await Task.Delay(NetworkRecoveryInterval!.Value + TimeSpan.FromSeconds(2));

            Assert.That(await GetConsumerCount(ReceiverQueue), Is.Zero, "The pump reconnected and started consuming after it was stopped.");
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
    }
}
