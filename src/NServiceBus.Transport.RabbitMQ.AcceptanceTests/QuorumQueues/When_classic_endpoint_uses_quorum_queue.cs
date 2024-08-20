namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_classic_endpoint_uses_quorum_queue : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_fail_to_start()
        {
            using (var connection = ConnectionHelper.ConnectionFactory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.DeclareQuorumQueue(Conventions.EndpointNamingConvention(typeof(ClassicQueueEndpoint)));
            }

            var exception = Assert.CatchAsync<Exception>(async () => await Scenario.Define<ScenarioContext>()
                .WithEndpoint<ClassicQueueEndpoint>()
                .Done(c => c.EndpointsStarted)
                .Run());

            Assert.That(exception.Message, Does.Contain("PRECONDITION_FAILED - inequivalent arg 'x-queue-type' for queue 'ClassicEndpointUsesQuorumQueue.ClassicQueueEndpoint'"));
            Assert.That(exception.Message, Does.Contain("received none but current is the value 'quorum'"));
        }

        class ClassicQueueEndpoint : EndpointConfigurationBuilder
        {
            public ClassicQueueEndpoint()
            {
                EndpointSetup<DefaultServer>();
            }
        }
    }
}