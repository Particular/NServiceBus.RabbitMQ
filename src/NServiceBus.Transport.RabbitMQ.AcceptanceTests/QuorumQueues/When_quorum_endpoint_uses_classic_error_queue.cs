namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NUnit.Framework;

    public class When_quorum_endpoint_uses_classic_error_queue : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_fail_to_start()
        {
            using (var connection = ConnectionHelper.ConnectionFactory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.DeclareClassicQueue("rabbitmq.transport.tests.classic-error");
            }


            var exception = Assert.CatchAsync<Exception>(async () => await Scenario.Define<ScenarioContext>()
                .WithEndpoint<QuorumQueueEndpoint>()
                .Done(c => c.EndpointsStarted)
                .Run());

            Assert.That(exception.Message, Does.Contain("PRECONDITION_FAILED - inequivalent arg 'x-queue-type' for queue 'rabbitmq.transport.tests.classic-error'"));
            Assert.That(exception.Message, Does.Contain("received the value 'quorum' of type 'longstr' but current is none'"));
        }

        class QuorumQueueEndpoint : EndpointConfigurationBuilder
        {
            public QuorumQueueEndpoint()
            {
                EndpointSetup<QuorumEndpoint>(config =>
                {
                    config.SendFailedMessagesTo("rabbitmq.transport.tests.classic-error");
                });
            }
        }
    }
}