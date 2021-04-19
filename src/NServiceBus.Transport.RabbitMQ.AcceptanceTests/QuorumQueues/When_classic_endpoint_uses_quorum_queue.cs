namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_classic_endpoint_uses_quorum_queue : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_fail_to_start()
        {
            await Scenario.Define<ScenarioContext>()
                .WithEndpoint<QuorumQueueEndpoint>()
                .Done(c => c.EndpointsStarted)
                .Run();

            var exception = Assert.CatchAsync<Exception>(async () => await Scenario.Define<ScenarioContext>()
                .WithEndpoint<ClassicQueueEndpoint>()
                .Done(c => c.EndpointsStarted)
                .Run());

            StringAssert.Contains("PRECONDITION_FAILED - inequivalent arg 'x-queue-type' for queue 'ClassicEndpointUsesQuorumQueue.ClassicQueueEndpoint' in vhost '/': received none but current is the value 'quorum'", exception.Message);
        }

        class ClassicQueueEndpoint : EndpointConfigurationBuilder
        {
            public ClassicQueueEndpoint()
            {
                EndpointSetup<DefaultServer>();
            }
        }

        class QuorumQueueEndpoint : EndpointConfigurationBuilder
        {
            public QuorumQueueEndpoint()
            {
                var clusterTemplate = new ClusterEndpoint(QueueMode.Quorum);
                EndpointSetup(clusterTemplate, (c, __) =>
                {
                    c.OverrideLocalAddress(Conventions.EndpointNamingConvention(typeof(ClassicQueueEndpoint)));
                    c.SendFailedMessagesTo("quorum-error");
                }, _ => { });
            }
        }
    }
}