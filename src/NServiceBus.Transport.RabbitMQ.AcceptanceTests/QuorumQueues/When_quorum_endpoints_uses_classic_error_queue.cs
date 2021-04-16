namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_quorum_endpoints_uses_classic_error_queue : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_fail_endpoint_startup()
        {
            await Scenario.Define<ScenarioContext>()
                .WithEndpoint<ClassicQueueEndpoint>()
                .Done(c => c.EndpointsStarted)
                .Run();

            var exception = Assert.CatchAsync<Exception>(async () =>
                await Scenario.Define<ScenarioContext>()
                    .WithEndpoint<QuorumQueueEndpoint>()
                    .Done(c => c.EndpointsStarted)
                    .Run());

            //TODO should we provide a more meaningful exception message?
            StringAssert.Contains("PRECONDITION_FAILED - inequivalent arg 'x-queue-type' for queue 'rabbitmq.transport.tests.classic-error' in vhost '/': received the value 'quorum'", exception.Message);
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