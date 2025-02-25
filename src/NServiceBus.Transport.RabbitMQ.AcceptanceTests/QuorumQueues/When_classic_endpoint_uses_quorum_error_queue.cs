namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_classic_endpoint_uses_quorum_error_queue : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_fail_to_start()
        {
            using (var connection = await ConnectionHelper.ConnectionFactory.CreateConnectionAsync())
            using (var channel = await connection.CreateChannelAsync())
            {
                await channel.DeclareQuorumQueue("rabbitmq.transport.tests.quorum-error");
            }

            var exception = Assert.CatchAsync<Exception>(async () => await Scenario.Define<ScenarioContext>()
                .WithEndpoint<ClassicQueueEndpoint>()
                .Done(c => c.EndpointsStarted)
                .Run());

            Assert.That(exception.Message, Does.Contain("PRECONDITION_FAILED - inequivalent arg 'x-queue-type' for queue 'rabbitmq.transport.tests.quorum-error'"));
            Assert.That(exception.Message, Does.Contain("received none but current is the value 'quorum'")  // RabbitMQ v3.x
                                            .Or.Contain("received 'classic' but current is 'quorum'"));  // RabbitMQ v4.x
        }

        class ClassicQueueEndpoint : EndpointConfigurationBuilder
        {
            public ClassicQueueEndpoint()
            {
                EndpointSetup<DefaultServer>(c => c
                    .SendFailedMessagesTo("rabbitmq.transport.tests.quorum-error"));
            }
        }
    }
}