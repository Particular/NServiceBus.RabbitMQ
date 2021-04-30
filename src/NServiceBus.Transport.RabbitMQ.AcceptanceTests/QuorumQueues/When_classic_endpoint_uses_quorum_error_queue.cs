namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_classic_endpoint_uses_quorum_error_queue : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_start_without_error()
        {
            using (var connection = ConnectionHelper.ConnectionFactory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.DeclareQuorumQueue("rabbitmq.transport.tests.quorum-error");
            }

            var context = await Scenario.Define<ScenarioContext>()
                .WithEndpoint<ClassicQueueEndpoint>()
                .Done(c => c.EndpointsStarted)
                .Run();

            Assert.IsTrue(context.EndpointsStarted);
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