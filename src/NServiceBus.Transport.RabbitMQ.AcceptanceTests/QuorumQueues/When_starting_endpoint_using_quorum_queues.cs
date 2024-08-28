namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Features;
    using global::RabbitMQ.Client.Exceptions;
    using NServiceBus.AcceptanceTests;
    using NUnit.Framework;

    public class When_starting_endpoint_using_quorum_queues : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_create_receiving_queues_as_quorum_queues()
        {
            var endpointInputQueue = Conventions.EndpointNamingConvention(typeof(QuorumQueueEndpoint));

            using (var connection = await ConnectionHelper.ConnectionFactory.CreateConnectionAsync())
            using (var channel = await connection.CreateChannelAsync())
            {
                await channel.QueueDeleteAsync(endpointInputQueue, false, false);
                await channel.QueueDeleteAsync(endpointInputQueue + "-disc", false, false);
                await channel.QueueDeleteAsync("QuorumQueueSatelliteReceiver", false, false);
            }

            await Scenario.Define<ScenarioContext>()
                .WithEndpoint<QuorumQueueEndpoint>()
                .Done(c => c.EndpointsStarted)
                .Run();

            // try to declare the same queue as a non-quorum queue, which should fail:
            using (var connection = await ConnectionHelper.ConnectionFactory.CreateConnectionAsync())
            {
                using (var channel = await connection.CreateChannelAsync())
                {
                    var mainQueueException = Assert.Catch<RabbitMQClientException>(async () => await channel.DeclareClassicQueue(endpointInputQueue));
                    Assert.That(mainQueueException.Message, Does.Contain("PRECONDITION_FAILED - inequivalent arg 'x-queue-type'"));
                }

                using (var channel = await connection.CreateChannelAsync())
                {
                    var instanceSpecificQueueException = Assert.Catch<RabbitMQClientException>(async () => await channel.DeclareClassicQueue(endpointInputQueue + "-disc"));
                    Assert.That(instanceSpecificQueueException.Message, Does.Contain("PRECONDITION_FAILED - inequivalent arg 'x-queue-type'"));
                }

                using (var channel = await connection.CreateChannelAsync())
                {
                    var satelliteReceiver = Assert.Catch<RabbitMQClientException>(async () => await channel.DeclareClassicQueue("QuorumQueueSatelliteReceiver"));
                    Assert.That(satelliteReceiver.Message, Does.Contain("PRECONDITION_FAILED - inequivalent arg 'x-queue-type'"));
                }
            }
        }

        public class QuorumQueueEndpoint : EndpointConfigurationBuilder
        {
            public QuorumQueueEndpoint()
            {
                EndpointSetup<QuorumEndpoint>(config =>
                {
                    config.SendFailedMessagesTo("error-quorum");
                    config.MakeInstanceUniquelyAddressable("disc");
                    config.EnableFeature<SatelliteFeature>();
                });
            }

            public class SatelliteFeature : Feature
            {
                protected override void Setup(FeatureConfigurationContext context)
                {
                    context.AddSatelliteReceiver("QuorumQueueSatelliteReceiver", new QueueAddress("QuorumQueueSatelliteReceiver"), PushRuntimeSettings.Default, (_, __) => RecoverabilityAction.Discard(string.Empty), (_, __, ___) => Task.CompletedTask);
                }
            }
        }
    }
}