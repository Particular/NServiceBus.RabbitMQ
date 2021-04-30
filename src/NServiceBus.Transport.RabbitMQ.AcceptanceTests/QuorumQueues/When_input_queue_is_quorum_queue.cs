namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_input_queue_is_quorum_queue : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_not_fail()
        {
            var queueName = Conventions.EndpointNamingConvention(typeof(ClassicQueueEndpoint));

            using (var connection = ConnectionHelper.ConnectionFactory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.DeclareQuorumQueue(queueName);
            }

            var context = await Scenario.Define<Context>()
                .WithEndpoint<ClassicQueueEndpoint>()
                .Done(c => c.EndpointsStarted)
                .Run();

            Assert.IsTrue(context.EndpointsStarted);
        }

        class Context : ScenarioContext
        {
        }

        class ClassicQueueEndpoint : EndpointConfigurationBuilder
        {
            public ClassicQueueEndpoint() => EndpointSetup<DefaultServer>();
        }
    }
}