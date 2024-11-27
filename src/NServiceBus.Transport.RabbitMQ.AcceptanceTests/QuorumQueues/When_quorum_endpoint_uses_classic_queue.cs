namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NUnit.Framework;

    public class When_quorum_endpoint_uses_classic_queue : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_fail_to_start()
        {
            using (var connection = await ConnectionHelper.ConnectionFactory.CreateConnectionAsync())
            using (var channel = await connection.CreateChannelAsync())
            {
                await channel.DeclareClassicQueue(Conventions.EndpointNamingConvention(typeof(QuorumQueueEndpoint)));
            }

            var exception = Assert.CatchAsync<Exception>(async () => await Scenario.Define<ScenarioContext>()
                .WithEndpoint<QuorumQueueEndpoint>()
                .Done(c => c.EndpointsStarted)
                .Run());

            Assert.That(exception.Message, Does.Contain("PRECONDITION_FAILED - inequivalent arg 'x-queue-type' for queue 'QuorumEndpointUsesClassicQueue.QuorumQueueEndpoint'"));
            Assert.That(exception.Message, Does.Contain("received the value 'quorum' of type 'longstr' but current is none'") // RabbitMQ v3.x
                                            .Or.Contain("received 'quorum' but current is 'classic'")); // RabbitMQ v4.x
        }

        class QuorumQueueEndpoint : EndpointConfigurationBuilder
        {
            public QuorumQueueEndpoint()
            {
                EndpointSetup<QuorumEndpoint>();
            }
        }
    }
}