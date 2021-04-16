namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Features;
    using global::RabbitMQ.Client.Exceptions;
    using NServiceBus.AcceptanceTests;
    using NUnit.Framework;

    public class When_using_quorum_queues : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_create_receiving_queues_as_quorum_queues()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<EndpointWithQuorumQueue>()
                .Done(c => c.EndpointsStarted)
                .Run();

            // try to declare the same queue as a non-quorum queue, which should fail:
            using (var connection = context.TransportConfiguration.CreateConnection())
            {
                using (var channel = connection.CreateModel())
                {
                    var mainQueueException = Assert.Catch<RabbitMQClientException>(() => channel.QueueDeclare(Conventions.EndpointNamingConvention(typeof(EndpointWithQuorumQueue)), true, false, false, new Dictionary<string, object>(0)));
                    StringAssert.Contains("PRECONDITION_FAILED - inequivalent arg 'x-queue-type'", mainQueueException.Message);
                }

                using (var channel = connection.CreateModel())
                {
                    var instanceSpecificQueueException = Assert.Catch<RabbitMQClientException>(() => channel.QueueDeclare(Conventions.EndpointNamingConvention(typeof(EndpointWithQuorumQueue)) + "-disc", true, false, false, new Dictionary<string, object>(0)));
                    StringAssert.Contains("PRECONDITION_FAILED - inequivalent arg 'x-queue-type'",
                        instanceSpecificQueueException.Message);
                }

                using (var channel = connection.CreateModel())
                {
                    var satelliteReceiver = Assert.Catch<RabbitMQClientException>(() => channel.QueueDeclare("QuorumQueueSatelliteReceiver", true, false, false, new Dictionary<string, object>(0)));
                    StringAssert.Contains("PRECONDITION_FAILED - inequivalent arg 'x-queue-type'", satelliteReceiver.Message);
                }
            }
        }

        class Context : ScenarioContext
        {
            public ClusterEndpoint TransportConfiguration { get; set; }
        }

        public class EndpointWithQuorumQueue : EndpointConfigurationBuilder
        {
            public EndpointWithQuorumQueue()
            {
                var defaultServer = new ClusterEndpoint(QueueMode.Quorum);
                EndpointSetup(
                    defaultServer,
                    (configuration, r) =>
                    {
                        // need to configure a different error queue that isn't a classic queue.
                        configuration.SendFailedMessagesTo("error-quorum");

                        // also test instance specific queues
                        configuration.MakeInstanceUniquelyAddressable("disc");

                        // also test satellite receivers
                        configuration.EnableFeature<SatelliteFeature>();

                        ((Context)r.ScenarioContext).TransportConfiguration = defaultServer;
                    },
                    _ => { });
            }

            public class SatelliteFeature : Feature
            {
                protected override void Setup(FeatureConfigurationContext context)
                {
                    context.AddSatelliteReceiver("QuorumQueueSatelliteReceiver", "QuorumQueueSatelliteReceiver", PushRuntimeSettings.Default, (_, __) => RecoverabilityAction.Discard(string.Empty), (_, __, ___) => Task.CompletedTask);
                }
            }
        }
    }


}