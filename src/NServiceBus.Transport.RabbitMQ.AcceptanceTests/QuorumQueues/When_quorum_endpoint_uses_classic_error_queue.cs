namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_quorum_endpoint_uses_classic_error_queue : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_not_fail_endpoint_startup()
        {
            await Scenario.Define<ScenarioContext>()
                .WithEndpoint<ClassicQueueEndpoint>()
                .Done(c => c.EndpointsStarted)
                .Run();

            await Scenario.Define<ScenarioContext>()
                .WithEndpoint<QuorumQueueEndpoint>()
                .Done(c => c.EndpointsStarted)
                .Run();
        }

        class ClassicQueueEndpoint : EndpointConfigurationBuilder
        {
            public ClassicQueueEndpoint()
            {
                EndpointSetup<DefaultServer>(c => c
                    .SendFailedMessagesTo("rabbitmq.transport.tests.classic-error"));
            }
        }

        class QuorumQueueEndpoint : EndpointConfigurationBuilder
        {
            public QuorumQueueEndpoint()
            {
                var clusterTemplate = new ClusterEndpoint(QueueMode.Quorum);
                EndpointSetup(clusterTemplate, (c, __) =>
                {
                    c.SendFailedMessagesTo("rabbitmq.transport.tests.classic-error");
                }, _ => { });
            }
        }
    }
}