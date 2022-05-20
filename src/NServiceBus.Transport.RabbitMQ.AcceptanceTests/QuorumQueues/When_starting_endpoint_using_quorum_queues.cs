namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Features;
    using global::RabbitMQ.Client.Exceptions;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_starting_endpoint_using_quorum_queues : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_create_receiving_queues_as_quorum_queues()
        {
            var endpointInputQueue = Conventions.EndpointNamingConvention(typeof(QuorumQueueEndpoint));

            using (var connection = ConnectionHelper.ConnectionFactory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDelete(endpointInputQueue, false, false);
                channel.QueueDelete(endpointInputQueue + "-disc", false, false);
                channel.QueueDelete("QuorumQueueSatelliteReceiver", false, false);
            }

            await Scenario.Define<ScenarioContext>()
                .WithEndpoint<QuorumQueueEndpoint>()
                .Done(c => c.EndpointsStarted)
                .Run();

            // try to declare the same queue as a non-quorum queue, which should fail:
            using (var connection = ConnectionHelper.ConnectionFactory.CreateConnection())
            {
                using (var channel = connection.CreateModel())
                {
                    var mainQueueException = Assert.Catch<RabbitMQClientException>(() => channel.DeclareClassicQueue(endpointInputQueue));
                    StringAssert.Contains("PRECONDITION_FAILED - inequivalent arg 'x-queue-type'", mainQueueException.Message);
                }

                using (var channel = connection.CreateModel())
                {
                    var instanceSpecificQueueException = Assert.Catch<RabbitMQClientException>(() => channel.DeclareClassicQueue(endpointInputQueue + "-disc"));
                    StringAssert.Contains("PRECONDITION_FAILED - inequivalent arg 'x-queue-type'", instanceSpecificQueueException.Message);
                }

                using (var channel = connection.CreateModel())
                {
                    var satelliteReceiver = Assert.Catch<RabbitMQClientException>(() => channel.DeclareClassicQueue("QuorumQueueSatelliteReceiver"));
                    StringAssert.Contains("PRECONDITION_FAILED - inequivalent arg 'x-queue-type'", satelliteReceiver.Message);
                }
            }
        }

        public class QuorumQueueEndpoint : EndpointConfigurationBuilder
        {
            public QuorumQueueEndpoint()
            {
                EndpointSetup<DefaultServer>(config =>
                {
                    config.UseTransport<RabbitMQTransport>().UseConventionalRoutingTopology(QueueType.Quorum);
                    config.SendFailedMessagesTo("error-quorum");
                    config.MakeInstanceUniquelyAddressable("disc");
                    config.EnableFeature<SatelliteFeature>();
                });
            }

            public class SatelliteFeature : Feature
            {
                protected override void Setup(FeatureConfigurationContext context)
                {
                    context.AddSatelliteReceiver("QuorumQueueSatelliteReceiver", "QuorumQueueSatelliteReceiver", PushRuntimeSettings.Default, (_, __) => RecoverabilityAction.Discard(string.Empty), (_, __) => Task.CompletedTask);
                }
            }
        }
    }
}